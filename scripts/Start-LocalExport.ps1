# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
<#
.SYNOPSIS
    Azurite を初期化し、B2C エクスポート コンソール アプリケーションをローカルで実行します。

.DESCRIPTION
    このスクリプトは以下を実行します:
    1. Azurite がインストールされているか確認
    2. Azurite BLOB サービスを既定ポート (10000) で開始
    3. 必要なストレージ コンテナーを作成
    4. ローカル設定で B2C エクスポート コンソール アプリケーションを実行

.PARAMETER ConfigFile
    設定ファイルへのパス（既定値: appsettings.local.json）

.PARAMETER VerboseLogging
    コンソール アプリケーションで詳細ログを有効にします

.PARAMETER SkipAzurite
    Azurite の初期化をスキップします（既に実行中の場合に使用）

.EXAMPLE
    .\Start-LocalExport.ps1

.EXAMPLE
    .\Start-LocalExport.ps1 -ConfigFile "appsettings.dev.json" -VerboseLogging

.EXAMPLE
    .\Start-LocalExport.ps1 -SkipAzurite
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$ConfigFile = "appsettings.local.json",

    [Parameter(Mandatory = $false)]
    [switch]$VerboseLogging,

    [Parameter(Mandatory = $false)]
    [switch]$SkipAzurite
)

$ErrorActionPreference = "Stop"

# スクリプト ディレクトリ
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$consoleAppDir = Join-Path $rootDir "src\B2CMigrationKit.Console"
$configPath = Join-Path $consoleAppDir $ConfigFile

# 出力用の色
 function Write-Success { Write-Host $args -ForegroundColor Green }
function Write-Info { Write-Host $args -ForegroundColor Cyan }
function Write-Warning { Write-Host $args -ForegroundColor Yellow }
function Write-Error { Write-Host $args -ForegroundColor Red }

# ヘッダー
Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  B2C Migration Kit - ローカル エクスポート ランナー" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# 設定ファイルの存在を確認
if (-not (Test-Path $configPath)) {
    Write-Error "設定ファイルが見つかりません: $configPath"
    Write-Info "設定ファイルを作成するか、-ConfigFile パラメーターで別のファイルを指定してください"
    exit 1
}

Write-Success "✓ 設定ファイルが見つかりました: $ConfigFile"

# 接続文字列を確認して Azurite が必要か自動検出
$needsAzurite = $false
if (-not $SkipAzurite) {
    try {
        $configContent = Get-Content $configPath -Raw | ConvertFrom-Json
        $connectionString = $configContent.Migration.Storage.ConnectionStringOrUri
        
        if ($connectionString -eq "UseDevelopmentStorage=true" -or 
            $connectionString -like "*127.0.0.1*" -or 
            $connectionString -like "*localhost*") {
            $needsAzurite = $true
            Write-Info "ローカル ストレージ エミュレーター設定を検出 - Azurite を開始します"
        }
        else {
            Write-Info "クラウド ストレージ設定を検出 - Azurite をスキップします"
            $SkipAzurite = $true
        }
    }
    catch {
        Write-Warning "⚠ ストレージタイプを検出するための設定ファイルの解析に失敗 - Azurite が必要と想定します"
        $needsAzurite = $true
    }
}

# Azurite がインストールされているか確認
if (-not $SkipAzurite -and $needsAzurite) {
    Write-Info "Azurite のインストールを確認しています..."

    $azuriteInstalled = $null
    try {
        $azuriteInstalled = Get-Command azurite -ErrorAction SilentlyContinue
    }
    catch {
        # Ignore error
    }

    if (-not $azuriteInstalled) {
        Write-Error "Azurite がインストールされていません！"
        Write-Info "Azurite をインストールするには: npm install -g azurite"
        Write-Info "別のストレージ エミュレーターを使用している場合は -SkipAzurite で実行してください"
        exit 1
    }

    Write-Success "✓ Azurite がインストールされています"

    # Azurite が既に実行中か確認
    Write-Info "Azurite が既に実行中か確認しています..."
    $azuriteProcess = Get-Process -Name "azurite" -ErrorAction SilentlyContinue

    if ($azuriteProcess) {
        Write-Warning "⚠ Azurite は既に実行中です (PID: $($azuriteProcess.Id))"
        Write-Info "既存の Azurite インスタンスを使用します"
    }
    else {
        Write-Info "Azurite を開始しています..."

        # Azurite 用のワークスペース ディレクトリを作成
        $azuriteWorkspace = Join-Path $rootDir ".azurite"
        if (-not (Test-Path $azuriteWorkspace)) {
            New-Item -ItemType Directory -Path $azuriteWorkspace | Out-Null
        }

        # Azurite をバックグラウンドで開始
        try {
            # cmd.exe 用に引数を単一の文字列として構築
            $azuriteCommand = "azurite --silent --location `"$azuriteWorkspace`" --blobPort 10000 --queuePort 10001"
            
            # ファイル関連付けの問題を回避するため cmd.exe を使用して azurite を実行
            $azuriteJob = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", $azuriteCommand -WindowStyle Hidden -PassThru

            # Azurite の開始を待機
            Write-Info "Azurite の開始を待機しています..."
            Start-Sleep -Seconds 5

            # Azurite が実行中か確認
            $azuriteProcess = Get-Process -Id $azuriteJob.Id -ErrorAction SilentlyContinue
            if (-not $azuriteProcess) {
                Write-Error "Azurite の開始に失敗しました - プロセスが予期せず終了しました"
                Write-Info "エラーを確認するには 'azurite' を手動で実行してみてください"
                exit 1
            }

            Write-Success "✓ Azurite が正常に開始されました (PID: $($azuriteProcess.Id))"
        }
        catch {
            Write-Error "Azurite の開始に失敗しました: $_"
            Write-Info "Azurite が正しくインストールされているか確認してください: npm install -g azurite"
            exit 1
        }
    }

    # ストレージ コンテナーを初期化
    Write-Info "ストレージ コンテナーを初期化しています..."

    try {
        # Azure CLI を使用してコンテナーを作成（az cli が必要）
        $azInstalled = Get-Command az -ErrorAction SilentlyContinue

        if ($azInstalled) {
            $connectionString = "UseDevelopmentStorage=true"

            # BLOB コンテナーを作成
            az storage container create --name "user-exports" --connection-string $connectionString --only-show-errors 2>&1 | Out-Null
            az storage container create --name "migration-errors" --connection-string $connectionString --only-show-errors 2>&1 | Out-Null

            # キューを作成
            az storage queue create --name "profile-updates" --connection-string $connectionString --only-show-errors 2>&1 | Out-Null

            Write-Success "✓ ストレージ コンテナーが初期化されました"
        }
        else {
            Write-Warning "⚠ Azure CLI が見つかりません - コンテナー作成をスキップします"
            Write-Info "コンテナーは初回使用時に自動的に作成されます"
        }
    }
    catch {
        Write-Warning "⚠ コンテナーの作成に失敗しました（既に存在するか、初回使用時に作成されます）"
    }
}
else {
    if ($SkipAzurite) {
        Write-Info "Azurite の初期化をスキップします（-SkipAzurite 使用またはクラウド ストレージ検出）"
    }
}

Write-Host ""
Write-Info "B2C エクスポートを開始しています..."
Write-Host ""

# コンソール アプリケーションの引数を構築
$appArgs = @("export", "--config", $ConfigFile)
if ($VerboseLogging) {
    $appArgs += "--verbose"
}

# コンソール アプリケーションを実行
try {
    Push-Location $consoleAppDir

    # 最初にプロジェクトをビルド
    Write-Info "コンソール アプリケーションをビルドしています..."
    dotnet build --configuration Debug --nologo --verbosity quiet

    if ($LASTEXITCODE -ne 0) {
        Write-Error "コンソール アプリケーションのビルドに失敗しました"
        exit 1
    }

    Write-Success "✓ ビルド成功"
    Write-Host ""

    # アプリケーションを実行
    dotnet run --no-build --configuration Debug -- $appArgs

    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

Write-Host ""

if ($exitCode -eq 0) {
    Write-Success "═══════════════════════════════════════════════"
    Write-Success "  エクスポートが正常に完了しました！"
    Write-Success "═══════════════════════════════════════════════"
}
else {
    Write-Error "═══════════════════════════════════════════════"
    Write-Error "  エクスポートが失敗しました（終了コード: $exitCode）"
    Write-Error "═══════════════════════════════════════════════"
}

Write-Host ""

# クリーンアップ指示
if (-not $SkipAzurite -and -not $azuriteProcess) {
    Write-Info "Azurite を停止するには: Stop-Process -Name azurite"
}

exit $exitCode
