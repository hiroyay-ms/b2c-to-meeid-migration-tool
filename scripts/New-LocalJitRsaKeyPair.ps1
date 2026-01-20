# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
<#
.SYNOPSIS
    JIT 認証テスト用の RSA キー ペアをローカルで生成します。

.DESCRIPTION
    ローカル開発/テスト用の RSA-2048 キー ペアを作成します。
    出力:
    - PEM 形式の秘密キー（local.settings.json インライン設定用）
    - JWK 形式の公開キー（参照用）
    - X.509 形式の公開キー（カスタム拡張機能アプリ登録用）
    
    ⚠️ 警告: ローカル テスト専用
    本番環境では Azure Key Vault を使用する New-JitRsaKeyPair.ps1 を使用してください。

.PARAMETER OutputPath
    キー ファイルが保存されるディレクトリ。既定値: カレント ディレクトリ

.EXAMPLE
    .\New-LocalJitRsaKeyPair.ps1
    
    カレント ディレクトリにキーを生成します

.EXAMPLE
    .\New-LocalJitRsaKeyPair.ps1 -OutputPath "./keys"
    
    ./keys ディレクトリにキーを生成します
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "."
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Yellow
Write-Host "  RSA キー ペア ジェネレーター - ローカル テスト モード" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Yellow
Write-Host ""
Write-Host "⚠️  警告: HSM 保護なしでキーをローカルに生成します" -ForegroundColor Red
Write-Host "   本番環境では Key Vault を使用する New-JitRsaKeyPair.ps1 を使用してください" -ForegroundColor Red
Write-Host ""

# 出力ディレクトリが存在することを確認
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

$OutputPath = Resolve-Path $OutputPath

Write-Host "RSA-2048 キー ペアを生成しています..." -ForegroundColor Cyan

# .NET を使用して RSA キー ペアを生成
Add-Type -AssemblyName System.Security

$rsa = [System.Security.Cryptography.RSA]::Create(2048)

# 秘密キーをエクスポート（PKCS#8 形式）
$privateKeyBytes = $rsa.ExportPkcs8PrivateKey()
$privateKeyBase64 = [Convert]::ToBase64String($privateKeyBytes)
$privateKeyPem = "-----BEGIN PRIVATE KEY-----`n"
for ($i = 0; $i -lt $privateKeyBase64.Length; $i += 64) {
    $length = [Math]::Min(64, $privateKeyBase64.Length - $i)
    $privateKeyPem += $privateKeyBase64.Substring($i, $length) + "`n"
}
$privateKeyPem += "-----END PRIVATE KEY-----"

# JWK 用の公開キー パラメーターをエクスポート
$publicKeyParams = $rsa.ExportParameters($false)

# バイト配列を Base64Url に変換（RFC 7515）
function ConvertTo-Base64Url {
    param([byte[]]$bytes)
    $base64 = [Convert]::ToBase64String($bytes)
    return $base64.TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$nBase64Url = ConvertTo-Base64Url $publicKeyParams.Modulus
$eBase64Url = ConvertTo-Base64Url $publicKeyParams.Exponent

# 一意のキー ID を生成
$kid = [Guid]::NewGuid().ToString()

# Graph API 用の自己署名証明書を作成（2年間有効）
Write-Host "自己署名証明書を生成しています..." -ForegroundColor Cyan
$cert = New-SelfSignedCertificate `
    -Subject "CN=JIT Migration Local Test" `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -NotAfter (Get-Date).AddYears(2) `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable `
    -KeyUsage KeyEncipherment, DataEncipherment `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")

# 証明書をファイルにエクスポート（DER 形式）
$certBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
$certBase64 = [Convert]::ToBase64String($certBytes)

# ストアから証明書を削除（一時的に必要だっただけ）
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force

# 公開キーのみもエクスポート（参照用）
$publicKeyBytes = $rsa.ExportSubjectPublicKeyInfo()
$publicKeyBase64 = [Convert]::ToBase64String($publicKeyBytes)

# 公開キー用の JWK（JSON Web Key）を作成
$publicKeyJwk = @{
    kty = "RSA"
    use = "enc"
    kid = $kid
    n = $nBase64Url
    e = $eBase64Url
} | ConvertTo-Json -Depth 5

# 秘密キーを保存
$privateKeyPath = Join-Path $OutputPath "jit-private-key.pem"
$privateKeyPem | Out-File -FilePath $privateKeyPath -Encoding ASCII -NoNewline

# 公開キーを保存（JWK）
$publicKeyJwkPath = Join-Path $OutputPath "jit-public-key.jwk.json"
$publicKeyJwk | Out-File -FilePath $publicKeyJwkPath -Encoding UTF8

# 証明書を保存（Graph API keyCredentials 用の X.509 DER base64）
$certPath = Join-Path $OutputPath "jit-certificate.txt"
$certBase64 | Out-File -FilePath $certPath -Encoding ASCII -NoNewline

# 公開キーのみを保存（参照用）
$publicKeyPath = Join-Path $OutputPath "jit-public-key-x509.txt"
$publicKeyBase64 | Out-File -FilePath $publicKeyPath -Encoding ASCII -NoNewline

Write-Host ""
Write-Host "✓ RSA キー ペアが正常に生成されました！" -ForegroundColor Green
Write-Host ""
Write-Host "作成されたファイル:" -ForegroundColor Cyan
Write-Host "  秘密キー (PEM):        $privateKeyPath" -ForegroundColor White
Write-Host "  公開キー (JWK):         $publicKeyJwkPath" -ForegroundColor White
Write-Host "  証明書 (X.509):         $certPath" -ForegroundColor White
Write-Host "  公開キー (X.509):       $publicKeyPath" -ForegroundColor Gray
Write-Host ""
Write-Host "キー ID (kid): $kid" -ForegroundColor Yellow
Write-Host "証明書:       $($cert.Subject)" -ForegroundColor Gray
Write-Host "有効期間:     $($cert.NotBefore.ToString('yyyy-MM-dd')) ～ $($cert.NotAfter.ToString('yyyy-MM-dd'))" -ForegroundColor Gray
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  次のステップ" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. 秘密キーを local.settings.json にコピー:" -ForegroundColor Yellow
Write-Host "   - 開く: $privateKeyPath"
Write-Host "   - 内容全体をコピー（BEGIN/END 行を含む）"
Write-Host "   - 貼り付け先: Migration__JitAuthentication__InlineRsaPrivateKey"
Write-Host "   - JSON 内で改行を \n リテラルに置換"
Write-Host ""
Write-Host "2. Manage-CustomAuthExtension.ps1 を使用してカスタム拡張機能を構成:" -ForegroundColor Yellow
Write-Host "   - スクリプトは自動的に jit-public-key-x509.txt を読み取ります"
Write-Host "   - 公開キーは Graph API にアップロードされます"
Write-Host ""
Write-Host "   例:" -ForegroundColor Gray
Write-Host "   .\Manage-CustomAuthExtension.ps1 -Operation Create ``" -ForegroundColor Cyan
Write-Host "       -NgrokUrl 'https://YOUR_NGROK_URL.ngrok.app' ``" -ForegroundColor Cyan
Write-Host "       -ApplyToAllApps" -ForegroundColor Cyan
Write-Host "   - Graph API を使用してアプリ登録にアップロード"
Write-Host "   - 参照: TESTING_PLAN.md Phase 4.2"
Write-Host ""
Write-Host "キー ID (kid): $kid" -ForegroundColor Magenta
Write-Host ""

# 機密オブジェクトをクリーンアップ
$rsa.Dispose()
