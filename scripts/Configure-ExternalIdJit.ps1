# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
<#
.SYNOPSIS
    Microsoft Entra External ID の Just-In-Time (JIT) パスワード移行を構成します。

.DESCRIPTION
    このスクリプトは、JIT パスワード移行を有効にするために、Microsoft Entra External ID の
    アプリ登録、カスタム認証拡張機能、およびイベント リスナーの構成を自動化します。
    
    スクリプトは以下の手順を実行します:
    1. デバイス コード フローを使用して認証（管理者権限を前提）
    2. カスタム認証拡張機能のアプリ登録を作成/構成
    3. 暗号化証明書の公開キーをエクスポートして構成
    4. カスタム拡張機能ポリシーを作成
    5. テスト用クライアント アプリケーションを作成
    6. イベント リスナー ポリシーを作成
    
    このスクリプトは External ID JIT 移行に関する Microsoft の公式ドキュメントに従っています。

.PARAMETER TenantId
    構成を適用する External ID テナント ID。

.PARAMETER CertificatePath
    パスワード ペイロードを暗号化するための証明書ファイル（.cer 形式）へのパス。
    Key Vault (JitMigrationEncryptionCert) からエクスポートしたものを使用してください。

.PARAMETER FunctionUrl
    JIT 認証用の Azure Function エンドポイントの URL。
    例: https://contoso.azurewebsites.net/api/JitAuthentication

.PARAMETER ExtensionAppName
    カスタム認証拡張機能のアプリ登録の名前。
    既定値: "EEID Auth Extension - JIT Migration"

.PARAMETER ClientAppName
    テスト用クライアント アプリケーションの名前。
    既定値: "JIT Migration Test Client"

.PARAMETER MigrationPropertyId
    移行ステータスを追跡するための拡張属性 ID。
    形式: extension_{ExtensionAppId}_RequiresMigration
    指定しない場合は、入力を求められます。

.PARAMETER SkipClientApp
    テスト用クライアント アプリケーションが既に存在する場合、作成をスキップします。

.EXAMPLE
    .\Configure-ExternalIdJit.ps1 -TenantId "your-tenant-id" `
        -CertificatePath "C:\certs\jitmigrationencryptioncert.cer" `
        -FunctionUrl "https://contoso.azurewebsites.net/api/JitAuthentication" `
        -MigrationPropertyId "extension_12345678_RequiresMigration"
    
    指定されたパラメーターで JIT 移行用に External ID を構成します。

.EXAMPLE
    .\Configure-ExternalIdJit.ps1 -TenantId "your-tenant-id" `
        -CertificatePath ".\cert.cer" `
        -FunctionUrl "https://contoso.azurewebsites.net/api/JitAuthentication" `
        -SkipClientApp
    
    クライアント アプリの作成をスキップして External ID を構成します。

.NOTES
    前提条件:
    - PowerShell 7.0 以降
    - External ID テナントの管理者権限を持つユーザー
    - CER 形式で Key Vault からエクスポートされた証明書
    - デプロイ済みでアクセス可能な Azure Function
    
    必要なアクセス許可（デバイス コード フロー中に付与）:
    - Application.ReadWrite.All
    - CustomAuthenticationExtension.ReadWrite.All
    - User.Read
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "External ID テナント ID")]
    [string]$TenantId,

    [Parameter(Mandatory = $true, HelpMessage = "証明書ファイル（.cer 形式）へのパス")]
    [ValidateScript({
        if (Test-Path $_) { $true }
        else { throw "証明書ファイルが見つかりません: $_" }
    })]
    [string]$CertificatePath,

    [Parameter(Mandatory = $true, HelpMessage = "JIT 認証用の Azure Function URL")]
    [ValidatePattern('^https://.*')]
    [string]$FunctionUrl,

    [Parameter(Mandatory = $false)]
    [string]$ExtensionAppName = "EEID Auth Extension - JIT Migration",

    [Parameter(Mandatory = $false)]
    [string]$ClientAppName = "JIT Migration Test Client",

    [Parameter(Mandatory = $false)]
    [string]$MigrationPropertyId,

    [Parameter(Mandatory = $false)]
    [switch]$SkipClientApp
)

$ErrorActionPreference = "Stop"

# ============================================================================
# ヘルパー関数
# ============================================================================

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Message)
    Write-Host "  → $Message" -ForegroundColor Gray
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Invoke-GraphRequest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Method,
        
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        
        [Parameter(Mandatory = $false)]
        [object]$Body,
        
        [Parameter(Mandatory = $true)]
        [string]$AccessToken
    )
    
    $headers = @{
        "Authorization" = "Bearer $AccessToken"
        "Content-Type" = "application/json"
    }
    
    $params = @{
        Method = $Method
        Uri = $Uri
        Headers = $headers
    }
    
    if ($Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 10)
    }
    
    try {
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        Write-ErrorMsg "Graph API リクエストが失敗しました: $($_.Exception.Message)"
        if ($_.Exception.Response) {
            $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "レスポンス: $responseBody" -ForegroundColor Red
        }
        throw
    }
}

function Get-DeviceCodeAccessToken {
    param(
        [string]$TenantId,
        [string[]]$Scopes
    )
    
    Write-Info "デバイス コード認証フローを開始しています..."
    Write-Step "テナント: $TenantId"
    Write-Step "スコープ: $($Scopes -join ', ')"
    Write-Host ""
    
    # デバイス コードを要求
    $clientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e"  # Microsoft Graph Command Line Tools
    $scopeString = ($Scopes -join ' ')
    
    $deviceCodeParams = @{
        Method = 'POST'
        Uri = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/devicecode"
        Body = @{
            client_id = $clientId
            scope = $scopeString
        }
    }
    
    try {
        $deviceCodeResponse = Invoke-RestMethod @deviceCodeParams
    }
    catch {
        Write-ErrorMsg "デバイス コードの取得に失敗しました: $($_.Exception.Message)"
        throw
    }
    
    # デバイス コードの手順を表示
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "  認証が必要です" -ForegroundColor Yellow
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host ""
    Write-Host $deviceCodeResponse.message -ForegroundColor White
    Write-Host ""
    Write-Host "認証を待機しています..." -ForegroundColor Gray
    Write-Host ""
    
    # トークンをポーリング
    $tokenParams = @{
        Method = 'POST'
        Uri = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/token"
        Body = @{
            grant_type = 'urn:ietf:params:oauth:grant-type:device_code'
            client_id = $clientId
            device_code = $deviceCodeResponse.device_code
        }
    }
    
    $timeout = [DateTime]::Now.AddSeconds($deviceCodeResponse.expires_in)
    $interval = $deviceCodeResponse.interval
    
    while ([DateTime]::Now -lt $timeout) {
        Start-Sleep -Seconds $interval
        
        try {
            $tokenResponse = Invoke-RestMethod @tokenParams
            Write-Success "認証に成功しました！"
            Write-Host ""
            return $tokenResponse.access_token
        }
        catch {
            $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
            if ($errorResponse.error -eq "authorization_pending") {
                # ユーザーの認証を待機中
                continue
            }
            elseif ($errorResponse.error -eq "authorization_declined") {
                Write-ErrorMsg "ユーザーによって認証が拒否されました"
                throw "認証が拒否されました"
            }
            elseif ($errorResponse.error -eq "expired_token") {
                Write-ErrorMsg "デバイス コードの有効期限が切れました"
                throw "デバイス コードの有効期限切れ"
            }
            else {
                Write-ErrorMsg "トークン リクエストが失敗しました: $($errorResponse.error_description)"
                throw
            }
        }
    }
    
    Write-ErrorMsg "認証がタイムアウトしました"
    throw "認証タイムアウト"
}

# ============================================================================
# メイン スクリプト
# ============================================================================

Write-Header "External ID JIT 移行構成"

Write-Info "構成パラメーター:"
Write-Step "Tenant ID: $TenantId"
Write-Step "Certificate: $CertificatePath"
Write-Step "Function URL: $FunctionUrl"
Write-Step "Extension App: $ExtensionAppName"
if (-not $SkipClientApp) {
    Write-Step "Client App: $ClientAppName"
}
Write-Host ""

# ステップ 0: デバイス コード フローを使用して認証
Write-Header "ステップ 1: 認証"

$requiredScopes = @(
    "https://graph.microsoft.com/Application.ReadWrite.All",
    "https://graph.microsoft.com/CustomAuthenticationExtension.ReadWrite.All",
    "https://graph.microsoft.com/User.Read"
)

$accessToken = Get-DeviceCodeAccessToken -TenantId $TenantId -Scopes $requiredScopes

# 現在のユーザーを取得して認証を確認
try {
    $me = Invoke-GraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/me" -AccessToken $accessToken
    Write-Success "認証済み: $($me.userPrincipalName)"
    Write-Host ""
}
catch {
    Write-ErrorMsg "認証の確認に失敗しました"
    exit 1
}

# ステップ 1: カスタム認証拡張機能のアプリ登録を作成または更新
Write-Header "ステップ 2: カスタム認証拡張機能アプリの構成"

Write-Info "アプリ登録を作成しています: $ExtensionAppName"

# アプリが既に存在するか確認
$existingApps = Invoke-GraphRequest -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/applications?`$filter=displayName eq '$ExtensionAppName'" `
    -AccessToken $accessToken

if ($existingApps.value.Count -gt 0) {
    Write-Warning "アプリ登録は既に存在します"
    $app = $existingApps.value[0]
    Write-Step "既存のアプリを使用: $($app.appId)"
} else {
    # 新しいアプリ登録を作成
    $appBody = @{
        displayName = $ExtensionAppName
        signInAudience = "AzureADMyOrg"
        requiredResourceAccess = @(
            @{
                resourceAppId = "00000003-0000-0000-c000-000000000000"  # Microsoft Graph
                resourceAccess = @(
                    @{
                        id = "214e68ed-7c8c-4ff7-a56c-6e60ae86e8cf"  # CustomAuthenticationExtension.Receive.Payload
                        type = "Role"
                    }
                )
            }
        )
    }
    
    $app = Invoke-GraphRequest -Method POST `
        -Uri "https://graph.microsoft.com/v1.0/applications" `
        -Body $appBody `
        -AccessToken $accessToken
    
    Write-Success "アプリ登録が作成されました"
    Write-Step "アプリ ID: $($app.appId)"
    Write-Step "オブジェクト ID: $($app.id)"
}

$extensionAppId = $app.appId
$extensionAppObjectId = $app.id

# Function URL からホスト名を抽出
$functionUri = [System.Uri]$FunctionUrl
$functionHostname = $functionUri.Host

# 識別子 URI を構成
Write-Info "識別子 URI を構成しています..."
$identifierUri = "api://$functionHostname/$extensionAppId"

$updateAppBody = @{
    identifierUris = @($identifierUri)
}

Invoke-GraphRequest -Method PATCH `
    -Uri "https://graph.microsoft.com/v1.0/applications/$extensionAppObjectId" `
    -Body $updateAppBody `
    -AccessToken $accessToken

Write-Success "識別子 URI が構成されました: $identifierUri"

# API アクセス許可の管理者同意を付与
Write-Info "API アクセス許可の管理者同意を付与しています..."
Write-Warning "Azure Portal で手動で管理者同意を付与してください:"
Write-Step "移動先: Azure Portal → アプリの登録 → $ExtensionAppName"
Write-Step "操作: API のアクセス許可 → [テナント名] に管理者の同意を与える"
Write-Host ""
$consent = Read-Host "管理者同意を付与した後、Enter を押してください（または 's' でスキップ）"
if ($consent -ne 's') {
    Write-Success "管理者同意が確認されました"
}

# ステップ 2: 暗号化証明書を構成
Write-Header "ステップ 3: 暗号化証明書の構成"

Write-Info "証明書を読み込んでいます: $CertificatePath"

try {
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertificatePath)
    $certBase64 = [Convert]::ToBase64String($cert.RawData)
    Write-Success "証明書が正常に読み込まれました"
    Write-Step "サブジェクト: $($cert.Subject)"
    Write-Step "有効開始日: $($cert.NotBefore)"
    Write-Step "有効終了日: $($cert.NotAfter)"
}
catch {
    Write-ErrorMsg "証明書の読み込みに失敗しました: $($_.Exception.Message)"
    exit 1
}

# キー用の新しい GUID を生成
$keyGuid = [guid]::NewGuid().ToString()

Write-Info "アプリ登録に暗号化キーを構成しています..."

$keyCredentialsBody = @{
    keyCredentials = @(
        @{
            endDateTime = $cert.NotAfter.ToString("yyyy-MM-ddTHH:mm:ssZ")
            keyId = $keyGuid
            startDateTime = $cert.NotBefore.ToString("yyyy-MM-ddTHH:mm:ssZ")
            type = "AsymmetricX509Cert"
            usage = "Encrypt"
            key = $certBase64
            displayName = "CN=JitMigration"
        }
    )
    tokenEncryptionKeyId = $keyGuid
}

Invoke-GraphRequest -Method PATCH `
    -Uri "https://graph.microsoft.com/v1.0/applications/$extensionAppObjectId" `
    -Body $keyCredentialsBody `
    -AccessToken $accessToken

Write-Success "暗号化証明書が構成されました"
Write-Step "キー ID: $keyGuid"

# ステップ 3: カスタム拡張機能ポリシーを作成
Write-Header "ステップ 4: カスタム認証拡張機能ポリシーの作成"

Write-Info "カスタム認証拡張機能を作成しています..."

$extensionBody = @{
    "@odata.type" = "#microsoft.graph.onPasswordSubmitCustomExtension"
    displayName = "OnPasswordSubmitCustomExtension"
    description = "Validate password"
    endpointConfiguration = @{
        "@odata.type" = "#microsoft.graph.httpRequestEndpoint"
        targetUrl = $FunctionUrl
    }
    authenticationConfiguration = @{
        "@odata.type" = "#microsoft.graph.azureAdTokenAuthentication"
        resourceId = $identifierUri
    }
    clientConfiguration = @{
        timeoutInMilliseconds = 2000
        maximumRetries = 1
    }
}

try {
    $customExtension = Invoke-GraphRequest -Method POST `
        -Uri "https://graph.microsoft.com/beta/identity/customAuthenticationExtensions" `
        -Body $extensionBody `
        -AccessToken $accessToken
    
    Write-Success "カスタム認証拡張機能が作成されました"
    Write-Step "拡張機能 ID: $($customExtension.id)"
    $customExtensionId = $customExtension.id
}
catch {
    Write-ErrorMsg "カスタム拡張機能の作成に失敗しました"
    Write-Info "拡張機能が既に存在するか確認しています..."
    
    $existingExtensions = Invoke-GraphRequest -Method GET `
        -Uri "https://graph.microsoft.com/beta/identity/customAuthenticationExtensions" `
        -AccessToken $accessToken
    
    $matchingExtension = $existingExtensions.value | Where-Object { $_.displayName -eq "OnPasswordSubmitCustomExtension" }
    
    if ($matchingExtension) {
        Write-Warning "既存のカスタム拡張機能を使用します"
        $customExtensionId = $matchingExtension.id
        Write-Step "拡張機能 ID: $customExtensionId"
        
        # 既存の拡張機能を更新
        Write-Info "カスタム拡張機能の構成を更新しています..."
        Invoke-GraphRequest -Method PATCH `
            -Uri "https://graph.microsoft.com/beta/identity/customAuthenticationExtensions/$customExtensionId" `
            -Body $extensionBody `
            -AccessToken $accessToken
        Write-Success "カスタム拡張機能が更新されました"
    }
    else {
        throw
    }
}

# ステップ 4: テスト用クライアント アプリケーションを作成
if (-not $SkipClientApp) {
    Write-Header "ステップ 5: テスト用クライアント アプリケーションの作成"
    
    Write-Info "クライアント アプリ登録を作成しています: $ClientAppName"
    
    # クライアント アプリが既に存在するか確認
    $existingClientApps = Invoke-GraphRequest -Method GET `
        -Uri "https://graph.microsoft.com/v1.0/applications?`$filter=displayName eq '$ClientAppName'" `
        -AccessToken $accessToken
    
    if ($existingClientApps.value.Count -gt 0) {
        Write-Warning "クライアント アプリは既に存在します"
        $clientApp = $existingClientApps.value[0]
        Write-Step "既存のアプリを使用: $($clientApp.appId)"
    } else {
        $clientAppBody = @{
            displayName = $ClientAppName
            signInAudience = "AzureADMyOrg"
            web = @{
                redirectUris = @("https://jwt.ms")
                implicitGrantSettings = @{
                    enableIdTokenIssuance = $true
                }
            }
            requiredResourceAccess = @(
                @{
                    resourceAppId = "00000003-0000-0000-c000-000000000000"  # Microsoft Graph
                    resourceAccess = @(
                        @{
                            id = "e1fe6dd8-ba31-4d61-89e7-88639da4683d"  # User.Read
                            type = "Scope"
                        }
                    )
                }
            )
        }
        
        $clientApp = Invoke-GraphRequest -Method POST `
            -Uri "https://graph.microsoft.com/v1.0/applications" `
            -Body $clientAppBody `
            -AccessToken $accessToken
        
        Write-Success "クライアント アプリが作成されました"
        Write-Step "アプリ ID: $($clientApp.appId)"
        Write-Step "オブジェクト ID: $($clientApp.id)"
        Write-Step "リダイレクト URI: https://jwt.ms"
    }
    
    $clientAppId = $clientApp.appId
    
    Write-Info "User.Read の管理者同意を付与しています..."
    Write-Warning "Azure Portal で手動で管理者同意を付与してください:"
    Write-Step "移動先: Azure Portal → アプリの登録 → $ClientAppName"
    Write-Step "操作: API のアクセス許可 → [テナント名] に管理者の同意を与える"
    Write-Host ""
    $consent = Read-Host "管理者同意を付与した後、Enter を押してください（または 's' でスキップ）"
    if ($consent -ne 's') {
        Write-Success "管理者同意が確認されました"
    }
} else {
    Write-Header "ステップ 5: クライアント アプリケーションの作成をスキップ"
    Write-Info "クライアント アプリは後で作成するか、既存のものを使用できます"
    
    # 既存のクライアント アプリ ID の入力を促す
    Write-Host ""
    $clientAppId = Read-Host "リスナー構成用のクライアント アプリ ID を入力してください（Enter でリスナー作成をスキップ）"
    
    if ([string]::IsNullOrWhiteSpace($clientAppId)) {
        Write-Warning "リスナー作成をスキップします - クライアント アプリ ID が提供されていません"
        $skipListener = $true
    }
}

# ステップ 5: イベント リスナー ポリシーを作成
if (-not $skipListener) {
    Write-Header "ステップ 6: イベント リスナー ポリシーの作成"
    
    # 移行プロパティ ID が提供されていない場合は入力を促す
    if ([string]::IsNullOrWhiteSpace($MigrationPropertyId)) {
        Write-Host ""
        Write-Info "イベント リスナーには移行プロパティ ID が必要です"
        Write-Step "形式: extension_{ExtensionAppId}_RequiresMigration"
        Write-Step "例: extension_12345678901234567890123456789012_RequiresMigration"
        Write-Host ""
        $MigrationPropertyId = Read-Host "移行プロパティ ID を入力してください"
        
        if ([string]::IsNullOrWhiteSpace($MigrationPropertyId)) {
            Write-ErrorMsg "移行プロパティ ID は必須です"
            exit 1
        }
    }
    
    Write-Info "クライアント アプリ用のイベント リスナーを作成しています: $clientAppId"
    Write-Step "移行プロパティ: $MigrationPropertyId"
    
    $listenerBody = @{
        "@odata.type" = "#microsoft.graph.onPasswordSubmitListener"
        conditions = @{
            applications = @{
                includeAllApplications = $false
                includeApplications = @(
                    @{
                        appId = $clientAppId
                    }
                )
            }
        }
        priority = 500
        handler = @{
            "@odata.type" = "#microsoft.graph.onPasswordMigrationCustomExtensionHandler"
            migrationPropertyId = $MigrationPropertyId
            customExtension = @{
                id = $customExtensionId
            }
        }
    }
    
    try {
        $listener = Invoke-GraphRequest -Method POST `
            -Uri "https://graph.microsoft.com/beta/identity/authenticationEventListeners" `
            -Body $listenerBody `
            -AccessToken $accessToken
        
        Write-Success "イベント リスナーが作成されました"
        Write-Step "リスナー ID: $($listener.id)"
    }
    catch {
        Write-ErrorMsg "イベント リスナーの作成に失敗しました"
        Write-Info "リスナーを手動で作成するか、既存のものを更新する必要がある場合があります"
        Write-Step "クライアント アプリ ID: $clientAppId"
        Write-Step "カスタム拡張機能 ID: $customExtensionId"
        Write-Step "移行プロパティ: $MigrationPropertyId"
    }
}

# サマリー
Write-Header "構成が完了しました！"

Write-Success "JIT 移行が正常に構成されました"
Write-Host ""

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  構成サマリー" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "カスタム拡張機能アプリ:" -ForegroundColor Cyan
Write-Step "アプリ ID: $extensionAppId"
Write-Step "オブジェクト ID: $extensionAppObjectId"
Write-Step "識別子 URI: $identifierUri"
Write-Host ""
Write-Host "カスタム認証拡張機能:" -ForegroundColor Cyan
Write-Step "拡張機能 ID: $customExtensionId"
Write-Step "ターゲット URL: $FunctionUrl"
Write-Host ""
if (-not $SkipClientApp -and $clientAppId) {
    Write-Host "テスト用クライアント アプリ:" -ForegroundColor Cyan
    Write-Step "アプリ ID: $clientAppId"
    Write-Step "リダイレクト URI: https://jwt.ms"
    Write-Host ""
}
if (-not $skipListener) {
    Write-Host "イベント リスナー:" -ForegroundColor Cyan
    Write-Step "移行プロパティ: $MigrationPropertyId"
    Write-Step "クライアント アプリ ID: $clientAppId"
    Write-Host ""
}

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
Write-Host "  次のステップ" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Azure Function の構成を確認:" -ForegroundColor White
Write-Step "Function App に JIT 認証の正しい設定があることを確認してください"
Write-Step "Function App が Key Vault から秘密キーにアクセスできることを確認してください"
Write-Host ""
Write-Host "2. 構成のテスト:" -ForegroundColor White
Write-Step "移動先: https://jwt.ms"
Write-Step "移行フラグが設定されたユーザーでサインインしてください"
Write-Step "JIT 関数が呼び出され、パスワードが検証されることを確認してください"
Write-Host ""
Write-Host "3. 監視と検証:" -ForegroundColor White
Write-Step "Azure Function のログでパスワード検証が成功していることを確認してください"
Write-Step "初回ログイン後にユーザーの移行ステータスが更新されることを確認してください"
Write-Step "Application Insights でエラーや問題を監視してください"
Write-Host ""
Write-Host "4. 本番デプロイ前:" -ForegroundColor White
Write-Step "複数のユーザー アカウントでテストしてください"
Write-Step "エラー処理とリトライ ロジックを確認してください"
Write-Step "すべてのセキュリティ要件が満たされていることを確認してください"
Write-Step "ロールバック計画を確認してテストしてください"
Write-Host ""

Write-Info "詳細なドキュメントはこちらを参照: https://learn.microsoft.com/entra/external-id"
Write-Host """
