日本語 | **[English](DEVELOPER_GUIDE.md)**

# B2C Migration Kit - 開発者ガイド

この包括的なガイドは、B2C から External ID への移行キットを実装、カスタマイズ、運用する開発者向けの詳細な情報を提供します。

## 目次

- [アーキテクチャ概要](#アーキテクチャ概要)
- [プロジェクト構造](#プロジェクト構造)
- [構成ガイド](#構成ガイド)
- [開発ワークフロー](#開発ワークフロー)
  - [ローカル開発セットアップ](#ローカル開発セットアップ)
  - [ソリューションのビルド](#ソリューションのビルド)
  - [テストの実行](#テストの実行)
  - [ngrok を使用した JIT Function のデバッグ](#ngrok-を使用した-jit-function-のデバッグ)
- [属性マッピング構成](#属性マッピング構成)
- [インポート監査ログ](#インポート監査ログ)
- [テスト戦略](#テスト戦略)
- [デプロイメント ガイド](#デプロイメント-ガイド)
- [運用とモニタリング](#運用とモニタリング)
- [セキュリティ ベスト プラクティス](#セキュリティ-ベスト-プラクティス)
- [トラブルシューティング](#トラブルシューティング)

## 概要と現在のフォーカス

> **📋 実装ステータス**: このリポジトリは、[Just-In-Time パスワード移行パブリック プレビュー](https://learn.microsoft.com/ja-jp/entra/external-id/customers/how-to-migrate-passwords-just-in-time?tabs=graph)の実装を例示することに焦点を当てています。現在の実装は、開発者がリファレンスとして使用できるエクスポート、インポート、JIT 認証機能の動作例を提供します。
>
> **将来のロードマップ**: インフラストラクチャ プロビジョニング用の Bicep/Terraform テンプレートを含む、Secure Future Initiative (SFI) 標準に準拠した自動デプロイメントは、今後のリリースで計画されています。現在のフォーカスは、本番環境の自動化ツールではなく、検証済みの移行パターンとコード例の提供にあります。

## アーキテクチャ概要

### 設計原則

移行キットは、以下の主要な原則を持つ SFI 準拠のモジュラー アーキテクチャ パターンに従います：

1. **関心の分離**: ビジネス ロジックは Core ライブラリ、ホスティングは Console/Function
2. **依存性注入**: テスタビリティのためにすべてのサービスは DI 経由で登録
3. **べき等性**: すべての操作を安全に再試行可能
4. **可観測性**: 包括的なテレメトリと構造化ログ
5. **セキュリティ**: 将来の本番環境デプロイメント向けの SFI 準拠設計パターン

### コンポーネント アーキテクチャ

```
B2CMigrationKit.Core/
├── Abstractions/          # サービス インターフェース
├── Models/                # ドメイン モデル
├── Configuration/         # 構成クラス
├── Services/
│   ├── Infrastructure/    # Azure サービス クライアント
│   ├── Observability/     # テレメトリ サービス
│   └── Orchestrators/     # 移行オーケストレーター
└── Extensions/            # DI 登録

B2CMigrationKit.Console/   # ローカル操作用 CLI
B2CMigrationKit.Function/  # JIT と同期用 Azure Function
```

## プロジェクト構造

### コア ライブラリ (`B2CMigrationKit.Core`)

**抽象化レイヤー**
- `IOrchestrator<T>` - オーケストレーションの基本インターフェース
- `IGraphClient` - Microsoft Graph 操作
- `IBlobStorageClient` - Blob Storage 操作
- `IQueueClient` - Queue Storage 操作
- `ITelemetryService` - テレメトリ操作
- `ICredentialManager` - マルチアプリ資格情報ローテーション
- `IAuthenticationService` - 資格情報検証

**モデル**
- `UserProfile` - ユーザー ID モデル
- `ExecutionResult` - 操作結果
- `BatchResult` - バッチ操作結果
- `PagedResult<T>` - ページング API 結果
- `MigrationStatus` - 移行状態列挙型
- `RunSummary` - 実行メトリクス

**サービス**

*インフラストラクチャ サービス*
- `GraphClient` - Polly リトライ ポリシーを使用した IGraphClient の実装
- `BlobStorageClient` - マネージド ID を使用した Blob 操作
- `QueueClient` - プロファイル同期用 Queue 操作 *（未実装）*
- `CredentialManager` - ラウンドロビン資格情報管理
- `AuthenticationService` - ROPC ベースの資格情報検証

*オーケストレーター*
- `ExportOrchestrator` - B2C ユーザー エクスポート
- `ImportOrchestrator` - External ID ユーザー インポート
- `JitMigrationService` - JIT 認証と移行
- `ProfileSyncService` - 非同期プロファイル同期 *（未実装）*

## 構成ガイド

### 構成構造

ツールキットは、ルートとして `MigrationOptions` を持つ階層的な構成を使用します：

```json
{
  "Migration": {
    "B2C": { ... },
    "ExternalId": { ... },
    "Storage": { ... },
    "Telemetry": { ... },
    "Retry": { ... },
    "BatchSize": 100
  }
}
```

### B2C 構成

```json
"B2C": {
  "TenantId": "your-b2c-tenant-id",
  "TenantDomain": "yourtenant.onmicrosoft.com",
  "RopcPolicyName": "B2C_1_ROPC",
  "AppRegistration": {
    "ClientId": "app-id-1",
    "ClientSecretName": "B2CAppSecret1",
    "Name": "B2C App 1",
    "Enabled": true
  },
  "Scopes": [ "https://graph.microsoft.com/.default" ]
}
```

**アプリ登録の要件：**
- **権限**: `Directory.Read.All`（エクスポート用）
- **認証**: クライアント資格情報フロー
- **シークレット**: ローカル開発では構成でクライアント シークレットを直接使用
- **スケーリング**: 異なる IP で異なるアプリ登録を持つ複数のインスタンスをデプロイ

### External ID 構成

```json
"ExternalId": {
  "TenantId": "your-external-id-tenant-id",
  "TenantDomain": "yourtenant.onmicrosoft.com",
  "ExtensionAppId": "00000000000000000000000000000000",
  "AppRegistration": {
    "ClientId": "app-id-1",
    "ClientSecretName": "ExternalIdAppSecret1",
    "Name": "External ID App 1",
    "Enabled": true
  },
  "PasswordPolicy": {
    "MinLength": 8,
    "RequireUppercase": true,
    "RequireLowercase": true,
    "RequireDigit": true,
    "RequireSpecialCharacter": true
  }
}
```

**アプリ登録の要件：**
- **権限**: `User.ReadWrite.All`、`Directory.ReadWrite.All`（インポート用）
- **拡張機能アプリ ID**: 拡張属性用のアプリケーション ID（ハイフンなし）
- **スケーリング**: 異なる IP で異なるアプリ登録を持つ複数のインスタンスをデプロイ

### Storage 構成

```json
"Storage": {
  "ConnectionStringOrUri": "https://yourstorage.blob.core.windows.net",
  "ExportContainerName": "user-exports",
  "ProfileSyncQueueName": "profile-updates",  // 将来のプロファイル同期機能用
  "UseManagedIdentity": true
}
```

**必要なロール：**
- Console/Function マネージド ID に必要：
  - `Storage Blob Data Contributor`
  - `Storage Queue Data Contributor`

### リトライ構成

```json
"Retry": {
  "MaxRetries": 5,
  "InitialDelayMs": 1000,
  "MaxDelayMs": 30000,
  "BackoffMultiplier": 2.0,
  "UseRetryAfterHeader": true,
  "OperationTimeoutSeconds": 120
}
```

### テレメトリ構成

ツールキットはデュアル テレメトリ出力をサポート：コンソール ログ（ローカル開発）と Application Insights（本番環境モニタリング）。

```json
"Telemetry": {
  "Enabled": true,
  "UseConsoleLogging": true,
  "UseApplicationInsights": false,
  "ConnectionString": "",
  "SamplingPercentage": 100.0,
  "TrackDependencies": true,
  "TrackExceptions": true
}
```

**構成オプション：**
- `Enabled` - すべてのテレメトリのマスター スイッチ
- `UseConsoleLogging` - テレメトリをコンソールに書き込み（ローカル開発に推奨）
- `UseApplicationInsights` - テレメトリを Azure App Insights に送信（本番環境）
- `ConnectionString` - App Insights 接続文字列（UseApplicationInsights=true の場合に必要）
- `SamplingPercentage` - コスト削減のためのサンプリング率（1.0-100.0）
- `TrackDependencies` - HTTP 呼び出し、データベース クエリを追跡
- `TrackExceptions` - ハンドルされていない例外を追跡

**一般的なシナリオ：**

*ローカル開発（コンソールのみ）：*
```json
{
  "UseConsoleLogging": true,
  "UseApplicationInsights": false
}
```

*本番環境モニタリング：*
```json
{
  "UseConsoleLogging": false,
  "UseApplicationInsights": true,
  "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=https://..."
}
```

*コスト最適化（10% サンプリング）：*
```json
{
  "UseApplicationInsights": true,
  "SamplingPercentage": 10.0
}
```

**テレメトリ メトリクス：**
- Export: `export.storage.total.bytes`、`export.throughput.users.per.second`
- Import: `import.graph.api.calls`、`import.blob.read.bytes`
- JIT: `JITAuth.PasswordValidated`、`JITAuth.MigrationSuccess`

## 開発ワークフロー

### ローカル開発セットアップ

1. **前提条件をインストール**
   ```bash
   # .NET 8.0 SDK
   dotnet --version  # 8.0 以上であること

   # Azure CLI（認証用）
   az login
   ```

2. **ローカル設定を構成**
   ```bash
   cd src/B2CMigrationKit.Console
   cp appsettings.json appsettings.Development.json
   # appsettings.Development.json を設定で編集
   ```

3. **ローカルでエクスポートを実行**
   ```powershell
   # リポジトリ ルートから - 自動化スクリプトを使用
   .\scripts\Start-LocalExport.ps1 -VerboseLogging
   ```
   
   スクリプトは自動的に：
   - 必要に応じて Azurite をチェックして開始
   - 必要なストレージ コンテナーを作成
   - コンソール アプリケーションをビルド
   - エクスポート操作を実行
   
   **手動代替方法**（Azurite を別途実行する必要がある）：
   ```powershell
   cd src\B2CMigrationKit.Console
   dotnet run -- export --config appsettings.Development.json --verbose
   ```

### ソリューションのビルド

```bash
# すべてのプロジェクトをビルド
dotnet build

# 特定のプロジェクトをビルド
dotnet build src/B2CMigrationKit.Core

# リリース用にビルド
dotnet build -c Release
```


### JIT（Just-In-Time）移行の実装

⏱️ **クイック スタート時間：** ローカル テスト実行まで 15 分

JIT 認証機能は、External ID カスタム認証拡張機能と統合して、ユーザーの初回ログイン試行時にパスワードを移行します。このセクションでは、ローカル開発から本番環境デプロイメントまでの完全な実装をカバーします。

---

#### 前提条件

**必要なツール：**
- .NET 8+ SDK
- Azure Functions Core Tools v4（`func --version`）
- ngrok（無料ティア：[ngrok.com](https://ngrok.com)）
- PowerShell 7+
- OpenSSL（RSA キー生成用）

**必要なアクセス：**
- テスト ユーザーを持つ Azure AD B2C テナント
- 管理者アクセスを持つ External ID テナント
- 既知のパスワードを持つテスト ユーザー

---

#### JIT トリガー メカニズムの理解

**重要な要件：** External ID は以下の場合**のみ** JIT 移行をトリガーします：
1. ユーザーが External ID に保存されているパスワードと**一致しない**パスワードを入力
2. かつ `extension_<appId>_RequiresMigration == true`

**なぜこれが重要か：**

一括インポート フェーズ中、`ImportOrchestrator` は各ユーザーに**一意の 16 文字のランダム パスワード**を生成します。これらはユーザーの実際の B2C パスワードでは**ありません**。この意図的な不一致により、初回ログイン時にパスワード検証が失敗し、JIT 移行フローがトリガーされます。

**ユーザー ログイン フロー：**

```
┌─────────────────────────────────────────────────────────────────┐
│                     インポート フェーズ                          │
├─────────────────────────────────────────────────────────────────┤
│  B2C ユーザー パスワード: "MyRealPassword123!"                  │
│                                                                 │
│  ImportOrchestrator が生成:                                     │
│  ランダム パスワード: "xK9#mP2qL8@vN4tR"（16 文字、一意）       │
│                                                                 │
│  External ID ユーザーが作成される:                              │
│  - ユーザー名: user@domain.com                                  │
│  - パスワード: "xK9#mP2qL8@vN4tR"（実際の B2C パスワードではない）│
│  - RequireMigration: true                                       │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                   初回ログイン（JIT トリガー）                   │
├─────────────────────────────────────────────────────────────────┤
│  ユーザーが入力: "MyRealPassword123!"（実際の B2C パスワード）  │
│                                                                 │
│  External ID が比較:                                            │
│  "MyRealPassword123!" ≠ "xK9#mP2qL8@vN4tR" → 不一致             │
│                                                                 │
│  かつ RequireMigration == true → JIT トリガー                   │
│                                                                 │
│  カスタム拡張機能が呼び出される:                                │
│  1. "MyRealPassword123!" を B2C ROPC に対して検証 ✓             │
│  2. External ID パスワードを "MyRealPassword123!" に更新        │
│  3. RequiresMigration = false を設定（移行完了）                │
│  4. ユーザー ログイン成功                                       │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                2 回目のログイン（通常フロー）                    │
├─────────────────────────────────────────────────────────────────┤
│  ユーザーが入力: "MyRealPassword123!"                           │
│                                                                 │
│  External ID が比較:                                            │
│  "MyRealPassword123!" == "MyRealPassword123!" → 一致            │
│                                                                 │
│  通常の認証 → JIT 呼び出しなし                                  │
│  即座にログイン成功                                             │
└─────────────────────────────────────────────────────────────────┘
```

**パスワード生成の実装：**

`ImportOrchestrator.cs`（行 598-638）に配置：

```csharp
private string GenerateRandomPassword()
{
    // 複雑性が保証された 16 文字のパスワード
    const int length = 16;
    const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string lowercase = "abcdefghijklmnopqrstuvwxyz";
    const string digits = "0123456789";
    const string special = "!@#$%^&*()-_=+[]{}|;:,.<>?";
    
    var password = new StringBuilder();
    
    // 各文字タイプを少なくとも 1 つ保証
    password.Append(uppercase[Random.Shared.Next(uppercase.Length)]);
    password.Append(lowercase[Random.Shared.Next(lowercase.Length)]);
    password.Append(digits[Random.Shared.Next(digits.Length)]);
    password.Append(special[Random.Shared.Next(special.Length)]);
    
    // 残りの文字を埋める
    string allChars = uppercase + lowercase + digits + special;
    for (int i = 4; i < length; i++)
    {
        password.Append(allChars[Random.Shared.Next(allChars.Length)]);
    }
    
    // パターンを防ぐためにシャッフル
    return new string(password.ToString().ToCharArray()
        .OrderBy(x => Random.Shared.Next()).ToArray());
}
```

**主な特徴：**
- ✅ **長さ：** 16 文字（ほとんどの複雑性要件を超える）
- ✅ **複雑性：** 大文字 1 + 小文字 1 + 数字 1 + 特殊文字 1 を保証
- ✅ **一意性：** 各ユーザーに新規生成（B2C データから派生しない）
- ✅ **ランダム性：** 予測可能なパターンを防ぐためにシャッフル
- ✅ **目的：** 初回ログイン時に JIT をトリガーするためのパスワード不一致を保証

---

#### ローカル開発セットアップ

**ステップ 1: RSA キー ペアを生成（5 分）**

**オプション A: 自動化スクリプトを使用（推奨）**
```powershell
.\scripts\New-JitRsaKeyPair.ps1 -OutputPath ".\B2C\local-keys"
```

**オプション B: OpenSSL で手動**
```bash
# 秘密キーを生成（2048 ビット RSA）
openssl genrsa -out private_key.pem 2048

# 公開キーを抽出
openssl rsa -in private_key.pem -pubout -out public_key.pem
```

**キーが作成されたことを確認：**
```powershell
Get-ChildItem .\B2C\local-keys\

# 期待される出力:
# private_key.pem  （RSA 秘密キー - Git にコミットしない）
# public_key.pem   （RSA 公開キー - 共有しても安全）
```

---

**ステップ 2: local.settings.json を構成**

`src/B2CMigrationKit.Function/local.settings.json` を作成または更新：

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet"
  },
  "Migration": {
    "JitAuthentication": {
      "UseKeyVault": false,
      "TestMode": true,
      "InlineRsaPrivateKey": "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC...\n-----END PRIVATE KEY-----",
      "TimeoutSeconds": 1.5,
      "CachePrivateKey": true
    },
    "B2C": {
      "TenantId": "your-b2c-tenant.onmicrosoft.com",
      "ClientId": "your-ropc-app-client-id",
      "ClientSecret": "your-client-secret",
      "PolicyName": "B2C_1_ROPC"
    },
    "ExternalId": {
      "TenantId": "your-external-id-tenant-id",
      "ClientId": "your-app-client-id",
      "ClientSecret": "your-client-secret",
      "ExtensionAppId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
    }
  }
}
```

**主な構成の注意事項：**
- **UseKeyVault: false** → ローカル開発ではインライン RSA キーを使用（v2.0 での Key Vault を使用した本番環境では true に設定）
- **TestMode: true** → B2C 検証をスキップ（B2C アクセスなしでテスト用）
- **InlineRsaPrivateKey** → ローカル開発用に秘密キーの内容全体を貼り付け（ヘッダーを含む）

---

**ステップ 3: ngrok で Function をローカル起動**

Function と ngrok トンネルの両方を処理する提供された PowerShell スクリプトを使用：

```powershell
cd src\B2CMigrationKit.Function
.\start-local.ps1
```

**スクリプトが行うこと：**
- Function をビルド
- 静的ドメイン（または構成されていない場合は動的）で ngrok トンネルを開始
- ポート 7071 で Azure Function を開始
- パブリック エンドポイント URL をクリップボードにコピー

**期待される出力：**
```
═══════════════════════════════════════════════
✅ ngrok Tunnel Active (Static Domain)
═══════════════════════════════════════════════

  Function URL: https://your-domain.ngrok-free.dev/api/JitAuthentication
  Static Domain: your-domain.ngrok-free.dev

✅ Function endpoint URL copied to clipboard!

Functions:
  JitAuthentication: [POST] http://localhost:7071/api/JitAuthentication
```

**手動代替方法**（自動化スクリプトなし）：
```powershell
# ターミナル 1: ngrok を開始
ngrok http 7071

# ターミナル 2: Function を開始
cd src\B2CMigrationKit.Function
func start
```

**✅ 成功インジケーター：**
- Function が `http://localhost:7071` で実行中
- パブリック HTTPS URL で ngrok トンネルがアクティブ
- RSA キーの欠落に関するエラーなし
- ログに "Using inline RSA private key" が表示される

---

**ステップ 4: カスタム認証拡張機能を構成**

**前提条件チェックリスト：**
- ✅ RSA キーが生成された（jit-private-key.pem、jit-public-key.jwk.json）
- ✅ Function local.settings.json がキーと資格情報で構成された
- ✅ RequiresMigration=true で External ID にユーザーがインポートされた
- ✅ External ID テナント管理者アクセス
- ✅ Function がローカルで実行中、ngrok トンネルがアクティブ

**サブステップ 1: カスタム拡張機能アプリ登録を作成**

1. **Azure Portal → External ID テナントに移動**
2. **移動先：** アプリの登録 → 新規登録
3. **構成：**
   - 名前: `Custom Authentication Extension - JIT Migration`
   - サポートされているアカウントの種類: `この組織ディレクトリのみに含まれるアカウント`
   - リダイレクト URI: 空白のまま
   - **登録**をクリック

4. **ID を記録：**
   ```
   アプリケーション (クライアント) ID: ______________________
   オブジェクト ID: ______________________
   ディレクトリ (テナント) ID: ______________________
   ```

5. **クライアント シークレットを作成：**
   - **証明書とシークレット**に移動
   - **クライアント シークレット** → **新しいクライアント シークレット**
   - 説明: `Custom Extension Secret`
   - 有効期限: 6 か月（テスト用）
   - **追加**をクリック
   - **すぐに値をコピー**

---

**サブステップ 2: RSA 公開キーをアップロード**

⚠️ **重要：** Azure Portal は UI 経由でのカスタム キーのアップロードをサポートしていません。Graph API を使用する必要があります。

```powershell
# 公開キー JWK を読み取り
$publicKeyPath = "c:\code\B2C Migration\scripts\jit-public-key.jwk.json"
$publicKeyJwk = Get-Content $publicKeyPath -Raw | ConvertFrom-Json

# カスタム拡張機能アプリの詳細（サブステップ 1 から）
$tenantId = "your-tenant-id"
$customExtensionAppObjectId = "PASTE_OBJECT_ID_HERE"

# 管理者トークンを取得
$token = (az account get-access-token --resource https://graph.microsoft.com --query accessToken -o tsv)

# キー資格情報を準備
$keyCred = @{
    type = "AsymmetricX509Cert"
    usage = "Verify"
    key = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($publicKeyJwk | ConvertTo-Json -Compress))
    displayName = "JIT Migration RSA Public Key"
    customKeyIdentifier = [System.Text.Encoding]::UTF8.GetBytes($publicKeyJwk.kid)
}

# アプリ登録にアップロード
$body = @{
    keyCredentials = @($keyCred)
    tokenEncryptionKeyId = $publicKeyJwk.kid
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Method Patch `
    -Uri "https://graph.microsoft.com/beta/applications/$customExtensionAppObjectId" `
    -Headers @{
        Authorization = "Bearer $token"
        "Content-Type" = "application/json"
    } `
    -Body $body

Write-Host "✓ 公開キーが正常にアップロードされました！" -ForegroundColor Green
```

---

**サブステップ 3: カスタム認証拡張機能リソースを作成**

```powershell
$tenantId = "your-tenant-id"
$ngrokUrl = "https://abc123.ngrok.app"
$customExtensionAppClientId = "PASTE_CLIENT_ID_HERE"

$token = (az account get-access-token --resource https://graph.microsoft.com --query accessToken -o tsv)

$extensionBody = @{
    "@odata.type" = "#microsoft.graph.onPasswordSubmitCustomExtension"
    displayName = "JIT Password Migration Extension - Local Testing"
    description = "B2C に対してパスワードを検証し、初回成功ログイン時にユーザーを移行"
    targetUrl = "$ngrokUrl/api/JitAuthentication"
    authenticationConfiguration = @{
        "@odata.type" = "#microsoft.graph.azureAdTokenAuthentication"
        resourceId = "api://$customExtensionAppClientId"
    }
} | ConvertTo-Json -Depth 10

$response = Invoke-RestMethod -Method Post `
    -Uri "https://graph.microsoft.com/beta/identity/customAuthenticationExtensions" `
    -Headers @{
        Authorization = "Bearer $token"
        "Content-Type" = "application/json"
    } `
    -Body $extensionBody

Write-Host "✓ カスタム拡張機能が正常に作成されました！" -ForegroundColor Green
Write-Host "Extension ID: $($response.id)" -ForegroundColor Cyan

$extensionId = $response.id
$extensionId | Out-File "custom-extension-id.txt"
```

---

**サブステップ 4: OnPasswordSubmit リスナー ポリシーを作成**

```powershell
$extensionAppId = "d7e9bb7927284f7c85d0fa045ec77b1f"  # ハイフンなし
$extensionId = Get-Content "custom-extension-id.txt"

# すべてのアプリケーションに適用（テストが容易）
$conditions = @{
    applications = @{
        includeAllApplications = $true
    }
}

$token = (az account get-access-token --resource https://graph.microsoft.com --query accessToken -o tsv)

$listenerBody = @{
    "@odata.type" = "#microsoft.graph.onPasswordSubmitListener"
    priority = 500
    conditions = $conditions
    handler = @{
        "@odata.type" = "#microsoft.graph.onPasswordMigrationCustomExtensionHandler"
        migrationPropertyId = "extension_${extensionAppId}_RequiresMigration"
        customExtension = @{
            id = $extensionId
        }
    }
} | ConvertTo-Json -Depth 10

$response = Invoke-RestMethod -Method Post `
    -Uri "https://graph.microsoft.com/beta/identity/authenticationEventListeners" `
    -Headers @{
        Authorization = "Bearer $token"
        "Content-Type" = "application/json"
    } `
    -Body $listenerBody

Write-Host "✓ 認証イベント リスナーが正常に作成されました！" -ForegroundColor Green
```

**検証チェックリスト：**
- [ ] カスタム拡張機能アプリが登録された
- [ ] RSA 公開キーがアップロードされた
- [ ] Azure Function がローカルで実行中
- [ ] ngrok トンネルがアクティブ
- [ ] カスタム拡張機能リソースが作成された
- [ ] 認証イベント リスナーが作成された
- [ ] RequiresMigration=true のテスト ユーザーが存在する

---

**ステップ 4: テスト ユーザーをインポート**

ランダム パスワードでユーザーを作成するためにインポートを実行：

```powershell
.\scripts\Start-LocalImport.ps1 -Verbose
```

**External ID で確認：**
- ユーザーが存在: `user@domain.com`
- `extension_<appId>_RequiresMigration == true`
- パスワードは実際の B2C パスワードではない

---

**ステップ 5: JIT フローをテスト**

**HTTP クライアントでテスト：**

`test-jit.http` を作成：
```http
POST https://abc123.ngrok.app/api/JitAuthentication
Content-Type: application/json

{
  "type": "customAuthenticationExtension",
  "data": {
    "authenticationContext": {
      "correlationId": "test-12345",
      "user": {
        "id": "user-object-id-from-external-id",
        "userPrincipalName": "testuser@yourdomain.com"
      }
    },
    "passwordContext": {
      "userPassword": "RealB2CPassword123!",
      "nonce": "test-nonce-value"
    }
  }
}
```

**期待されるレスポンス（TestMode=true）：**
```json
{
  "data": {
    "actions": [
      {
        "@odata.type": "microsoft.graph.customAuthenticationExtension.migratePassword"
      }
    ]
  }
}
```

---

#### VS Code デバッグ セットアップ

1. **`.vscode/launch.json` を作成：**

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Attach to .NET Functions",
      "type": "coreclr",
      "request": "attach",
      "processId": "${command:pickProcess}"
    }
  ]
}
```

2. **スクリプトで Function を開始：**
```powershell
cd src\\B2CMigrationKit.Function
.\\start-local.ps1
```

3. **デバッガーをアタッチ：**
   - `JitAuthenticationFunction.cs` または `JitMigrationService.cs` を開く
   - ブレークポイントを設定（F9）
   - F5 を押す → "Attach to .NET Functions" を選択
   - `func` または `dotnet` プロセスを見つけて選択

4. **推奨ブレークポイント：**
   - `JitAuthenticationFunction.cs:60` - External ID ペイロードを解析
   - `JitAuthenticationFunction.cs:123` - JitMigrationService を呼び出し
   - `JitMigrationService.cs:73` - ユーザーを取得して移行ステータスを確認
   - `JitMigrationService.cs:125` - ROPC 経由で B2C に対して資格情報を検証
   - `JitMigrationService.cs:156` - パスワード複雑性を検証
   - `JitMigrationService.cs:193` - ユーザー拡張属性を更新

---

#### ngrok Web インターフェース

リクエスト検査のために ngrok Web インターフェースにアクセス：

```
http://localhost:4040
```

**機能：**
- Function へのすべての HTTP リクエストを表示
- リクエスト/レスポンス ヘッダーとボディを検査
- **リクエストをリプレイ** - ログイン フローをやり直さずにエラーを再現
- パス（`/api/JitAuthentication`）またはステータス コードでフィルタリング

---

#### リクエスト フローの理解

**External ID カスタム認証拡張機能フロー：**

```
1. ユーザーが External ID にログイン
   ↓
2. External ID がユーザーの存在を検証
   ↓
3. External ID がペイロードで JIT Function を呼び出し:
   {
     "data": {
       "authenticationContext": {
         "user": {
           "id": "user-object-id",
           "userPrincipalName": "user@tenant.com"
         },
         "correlationId": "correlation-id"
       },
       "passwordContext": {
         "userPassword": "user-entered-password"
       }
     }
   }
   ↓
4. JIT Function が検証してアクションを返す:
   {
     "data": {
       "@odata.type": "microsoft.graph.onPasswordSubmitResponseData",
       "actions": [{
         "@odata.type": "microsoft.graph.passwordsubmit.MigratePassword"
       }]
     }
   }
   ↓
5. External ID がパスワードを更新してログインを完了
```

---

#### ログ パターン

**成功した移行：**
```
[JIT Function] HTTP POST received | RequestId: req-abc123
[JIT Function] Parsed External ID payload | UserId: user-obj-id | UPN: testuser@...
[JIT Migration] Starting | UserId: user-obj-id | CorrelationId: corr-xyz
[JIT Migration] Step 1/3: Checking migration status
[JIT Migration] ✓ User needs migration - Proceeding
[JIT Migration] Step 2/3: Validating credentials against B2C via ROPC
[JIT Migration] ✓ B2C credentials validated successfully
[JIT Migration] Step 3/3: Validating password complexity
[JIT Migration] ✓ Password complexity validated
[JIT Migration] ✅ SUCCESS - Returning MigratePassword action | Duration: 1250ms
```

**既に移行済み（高速パス）：**
```
[JIT Migration] Step 1/3: Checking migration status
[JIT Migration] ✓ User already migrated - Allowing login | Duration: 450ms
```

**無効な資格情報：**
```
[JIT Migration] Step 2/3: Validating credentials against B2C via ROPC
[JIT Migration] ❌ FAILED - B2C credential validation failed
```

---

#### 本番環境デプロイメント

> **⚠️ 重要**: セキュアな証明書管理と自動インフラストラクチャ プロビジョニングを備えた本番環境デプロイメントは、**v2.0 で完全に実装および検証される予定**です。
>
> **現在のリリース (v1.0)**:
> - ✅ 自己署名証明書とインライン シークレット（gitignore された構成ファイル）を使用したローカル開発
> - ✅ ngrok を使用した開発テストと検証
>
> **将来のリリース (v2.0)**:
> - 🔜 セキュアな証明書管理の自動化
> - 🔜 Azure Function 用マネージド ID
> - 🔜 本番環境 Azure Function デプロイメント テンプレート
> - 🔜 SFI に準拠した自動インフラストラクチャ デプロイメント

---

#### JIT トラブルシューティング

**問題: JIT がトリガーされない**

**症状:** ユーザーが正しい B2C パスワードを入力しても JIT 呼び出しが発生しない

**解決策：**
```powershell
# ユーザーがランダム パスワード（実際の B2C パスワードではない）を持っていることを確認
Get-MgUser -UserId "user@domain.com" | Select-Object PasswordProfile

# RequireMigration ステータスを確認
Get-MgUser -UserId "user@domain.com" -Property "extension_*" | 
    Select-Object -ExpandProperty AdditionalProperties

# カスタム拡張機能が割り当てられていることを確認
Get-MgIdentityAuthenticationEventsFlow
```

---

**問題: ngrok URL が再起動時に変わる**

**解決策：**

自動化でクイック更新：
```powershell
.\scripts\Setup-JitCustomExtension.ps1 `
    -TenantId "your-tenant-id" `
    -NgrokUrl "https://NEW-URL.ngrok.app" `
    -PublicKeyPath ".\keys\public_key.pem" `
    -ExtensionAppId "existing-app-id"
```

または静的ドメイン用の ngrok 有料プランを使用：
```powershell
ngrok http 7071 --domain=myapp.ngrok.app
```

---

**問題: Function タイムアウト（2 秒）**

**構成を最適化：**
```json
{
  "Migration": {
    "JitAuthentication": {
      "TimeoutSeconds": 1.5,
      "CachePrivateKey": true
    },
    "Retry": {
      "MaxRetries": 1,
      "DelaySeconds": 0.1
    }
  }
}
```

**パフォーマンスを監視：**
```kusto
requests
| where name == "JitAuthentication"
| summarize avg(duration), max(duration), percentile(duration, 95)
```

**目標: 95 パーセンタイルで 1500ms 未満**

---

**問題: 本番環境でテスト モードが有効**

⚠️ **セキュリティ警告:** 本番環境での TestMode=true：
- B2C 資格情報検証をスキップ（任意のパスワードが受け入れられる）
- パスワード複雑性チェックをスキップ
- 不正アクセスを許可
- **本番環境では決して使用しない**

**解決策：**
```powershell
az functionapp config appsettings set `
    --name my-function `
    --resource-group my-rg `
    --settings "Migration__JitAuthentication__TestMode=false"
```

---

#### JIT 構成リファレンス

**ローカル開発：**
```json
{
  "Migration": {
    "JitAuthentication": {
      "UseKeyVault": false,
      "TestMode": true,
      "InlineRsaPrivateKey": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----",
      "TimeoutSeconds": 1.5,
      "CachePrivateKey": true
    }
  }
}
```

> **注意**: 本番環境構成は、自動デプロイメント テンプレートと共に v2.0 で文書化されます。

#### 一般的な JIT デバッグ シナリオ

**シナリオ: ユーザーが見つからない**
- ペイロードの userId を確認: `[JIT Function] Parsed External ID payload | UserId: ...`
- ユーザーの存在を確認: `az ad user show --id "<userId>"`
- アプリ登録の権限を確認

**シナリオ: B2C 資格情報検証が失敗**
- ROPC ポリシーが存在することを確認: `B2C_1_ROPC`
- B2C ログインを直接テスト：
  ```bash
  curl -X POST https://b2cprod.b2clogin.com/b2cprod.onmicrosoft.com/B2C_1_ROPC/oauth2/v2.0/token \
    -d "grant_type=password" \
    -d "username=test@b2cprod.onmicrosoft.com" \
    -d "password=Test123!@#" \
    -d "client_id=<client-id>" \
    -d "scope=openid"
  ```
- External ID と B2C 間の UPN 変換を確認

**シナリオ: パスワード複雑性が失敗**
- `local.settings.json` のパスワード ポリシーを確認
- パスワードに以下があることを確認: 8 文字以上、大文字、小文字、数字、特殊文字
- `JitMigrationService.cs:156` にブレークポイントを設定
- ユーザーは SSPR 経由でパスワードをリセットする必要がある

**シナリオ: Graph API スロットリング（HTTP 429）**
- Graph API 制限: アプリ登録あたり約 60 ops/sec
- リトライ ログを表示: `[GraphClient] Request throttled (429/503) - Retrying in X ms...`
- 負荷テストでは、リクエスト間に遅延を追加

#### JIT デバッグのヒント

- **ngrok リプレイを使用**してエラーを素早く再現
- **CorrelationId でログをフィルタリング**してエンドツーエンド操作をトレース
- **条件付きブレークポイントを使用**: ブレークポイントを右クリック → `userPrincipalName.Contains("testuser")`
- **ngrok Web UI を監視**（localhost:4040）してすべてのリクエストをリアルタイムで確認
- **コード変更後に再ビルド**: `dotnet build src/B2CMigrationKit.Function`

---

## 属性マッピング構成

### 概要

Azure AD B2C と Entra External ID は両方とも同じ Microsoft Graph User オブジェクト モデルを使用します。ほとんどの属性はマッピングなしで直接コピーできます。ただし、以下が必要な場合があります：

1. テナント間で異なる名前を持つ**カスタム拡張属性をマッピング**
2. コピーから**特定のフィールドを除外**
3. **移行固有の属性を構成**（B2CObjectId、RequireMigration）

### 構成構造

#### エクスポート構成

B2C からエクスポートされるフィールドを制御：

```json
{
  "Migration": {
    "Export": {
      "SelectFields": "id,userPrincipalName,displayName,givenName,surname,mail,mobilePhone,identities,extension_abc123_CustomerId"
    }
  }
}
```

**デフォルト フィールド：**
- `id` - ユーザーの ObjectId（必須）
- `userPrincipalName` - UPN
- `displayName` - 表示名
- `givenName` - 名
- `surname` - 姓
- `mail` - メール アドレス
- `mobilePhone` - 携帯電話
- `identities` - すべてのユーザー ID

**カスタム拡張属性を追加するには：**
`SelectFields` のカンマ区切りリストに追加します。例：
```
"SelectFields": "id,userPrincipalName,displayName,...,extension_abc123_CustomerId,extension_abc123_Department"
```

#### インポート構成

External ID への属性のインポート方法を制御：

```json
{
  "Migration": {
    "Import": {
      "AttributeMappings": {
        "extension_abc123_LegacyId": "extension_xyz789_CustomerId"
      },
      "ExcludeFields": ["createdDateTime"],
      "MigrationAttributes": {
        "StoreB2CObjectId": true,
        "B2CObjectIdTarget": "extension_xyz789_OriginalB2CId",
        "SetRequiresMigration": true,
        "RequiresMigrationTarget": "extension_xyz789_RequiresMigration"
      }
    }
  }
}
```

##### AttributeMappings

ソース属性名を異なるターゲット名にマッピングします。

**キー** = B2C のソース属性名
**値** = External ID のターゲット属性名

例：
```json
"AttributeMappings": {
  "extension_b2c_app_LegacyCustomerId": "extension_extid_app_CustomerId",
  "extension_b2c_app_Department": "extension_extid_app_DepartmentCode"
}
```

**動作：**
- 属性がマッピングにある場合: ターゲット名に名前変更
- 属性がマッピングにない場合: そのままコピー（同じ名前）
- 明示的にマッピングまたは除外されていないすべての属性は変更なしでコピー

##### ExcludeFields

インポートから除外するフィールド名のリスト。これらのフィールドは External ID にコピーされません。

```json
"ExcludeFields": [
  "createdDateTime",
  "lastPasswordChangeDateTime",
  "extension_abc123_TemporaryField"
]
```

##### MigrationAttributes

移行固有の属性を制御：

**StoreB2CObjectId**（bool、デフォルト: `true`）
- 元の B2C ObjectId を External ID に保存するかどうか
- 相関とトラブルシューティングに役立つ
- この追跡が不要な場合は `false` に設定

**B2CObjectIdTarget**（string、オプション）
- B2C ObjectId を保存するためのターゲット属性名
- デフォルト: `extension_{ExtensionAppId}_B2CObjectId`
- `StoreB2CObjectId` が `true` の場合にのみ使用

**SetRequiresMigration**（bool、デフォルト: `true`）
- RequiresMigration フラグを設定するかどうか
- JIT 認証がパスワード移行が必要かどうかを知るために使用
- デフォルトでは `true` に設定（パスワードはまだ移行されていない）
- 異なる移行追跡メカニズムを使用している場合は `false` に設定

**RequiresMigrationTarget**（string、オプション）
- RequiresMigration フラグのターゲット属性名
- デフォルト: `extension_{ExtensionAppId}_RequiresMigration`
- `SetRequiresMigration` が `true` の場合にのみ使用

### 一般的なマッピング シナリオ

#### シナリオ 1: シンプルな移行（カスタム属性なし）

デフォルト構成を使用 - マッピング不要：

```json
{
  "Export": {
    "SelectFields": "id,userPrincipalName,displayName,givenName,surname,mail,mobilePhone,identities"
  },
  "Import": {
    "AttributeMappings": {},
    "ExcludeFields": [],
    "MigrationAttributes": {
      "StoreB2CObjectId": true,
      "SetRequiresMigration": true
    }
  }
}
```

#### シナリオ 2: 異なる拡張属性名

B2C と External ID 間で属性名が異なる場合：

```json
{
  "Export": {
    "SelectFields": "id,userPrincipalName,...,extension_b2c_CustomerId"
  },
  "Import": {
    "AttributeMappings": {
      "extension_b2c_CustomerId": "extension_extid_LegacyUserId"
    }
  }
}
```

`extension_b2c_CustomerId` はインポート中に `extension_extid_LegacyUserId` に名前変更されます。

#### シナリオ 3: 複数のカスタム属性を持つ複雑なマッピング

```json
{
  "Export": {
    "SelectFields": "id,userPrincipalName,displayName,givenName,surname,mail,mobilePhone,identities,extension_abc_CustomerId,extension_abc_Department,extension_abc_EmployeeType,extension_abc_CostCenter"
  },
  "Import": {
    "AttributeMappings": {
      "extension_abc_CustomerId": "extension_xyz_LegacyId",
      "extension_abc_Department": "extension_xyz_DeptCode",
      "extension_abc_EmployeeType": "extension_xyz_UserType"
    },
    "ExcludeFields": [
      "extension_abc_CostCenter"
    ],
    "MigrationAttributes": {
      "StoreB2CObjectId": true,
      "B2CObjectIdTarget": "extension_xyz_B2COriginalId",
      "SetRequiresMigration": true,
      "RequiresMigrationTarget": "extension_xyz_RequiresMigration"
    }
  }
}
```

この構成：
- 4 つのカスタム拡張属性をエクスポート
- 3 つを異なる名前にマッピング
- インポートから `CostCenter` を除外
- B2C ObjectId を `extension_xyz_B2COriginalId` として保存
- 移行フラグを `extension_xyz_Migrated` として設定

### 属性マッピングに関する重要な注意事項

#### 1. 最初に拡張属性を作成

インポート前に、すべてのターゲット カスタム属性が External ID テナントに存在することを確認：

1. **Azure Portal** → **External Identities** → **カスタム ユーザー属性**に移動
2. 使用する予定の各カスタム属性を作成
3. 完全な属性名をメモ: `extension_{appId}_{attributeName}`

#### 2. 拡張機能アプリ ID

`ExtensionAppId`（ハイフンなし）は完全な属性名を構築するために使用：

```json
{
  "ExternalId": {
    "ExtensionAppId": "abc123def456..."  // ハイフンなし！
  }
}
```

完全な属性名形式: `extension_{ExtensionAppId}_{attributeName}`

#### 3. 標準 User オブジェクト フィールド

標準 Graph API User フィールドは自動的にコピー（エクスポートに含まれている場合）：
- displayName、givenName、surname
- mail、mobilePhone、otherMails
- streetAddress、city、state、postalCode、country
- userPrincipalName、identities
- accountEnabled

非標準シナリオを使用していない限り、これらはマッピングを必要としません。

#### 4. 自動変換

インポート プロセスは自動的に処理：
- **UPN ドメイン更新**: `user@b2c.onmicrosoft.com` を `user@externalid.onmicrosoft.com` に変更
- **ID 発行者更新**: B2C ドメインから External ID ドメインに ID 発行者を更新
- **パスワード置換**: JIT 移行用のランダム プレースホルダー パスワードを設定

### UPN とメール ID 変換

**背景**: Entra External ID は Azure AD B2C より厳格な検証を適用：
- UPN は External ID テナント ドメインに属している必要がある
- すべてのユーザーは `emailAddress` ID を持つ必要がある（OTP とパスワード リセットに必要）
- B2C はメール アドレスなしのユーザーを許可、External ID は許可しない

**自動変換ロジック**:

インポート オーケストレーターは以下の変換を自動的に適用：

#### 1. UPN ドメイン変換

**コードの場所**: `ImportOrchestrator.cs:TransformUpnForExternalId()`

**目的**: JIT 認証を可能にするために**ローカル パート識別子を保持**しながら、UPN ドメインを B2C から External ID に変更します。このアプローチは、Entra External ID での JIT パスワード移行**中**に[サインイン エイリアス](https://learn.microsoft.com/ja-jp/entra/external-id/customers/how-to-sign-in-alias)機能の使用を可能にする回避策として機能します。

**注意**: この実装は、まったく新しい UPN を作成する公式 Microsoft ドキュメント アプローチとは異なります。UPN のローカル パートを保持することで、両方のテナント間でユーザー識別子の継続性を維持し、シームレスな JIT 認証と移行プロセス中のサインイン エイリアス シナリオをサポートします。

```csharp
// 元の B2C UPN
user.UserPrincipalName = "user#EXT#@b2cprod.onmicrosoft.com"

// 変換ステップ:
// 1. ローカル パートを抽出（@ の前）: "user#EXT#"
// 2. #EXT# マーカーを削除: "user"
// 3. ローカル パートからアンダースコアとドットを削除: "user"（この場合は変更なし）
// 4. ドメインを External ID テナント ドメインに置換
// 5. クリーニング後にローカル パートが空の場合、GUID ベースの識別子を生成

// 結果
user.UserPrincipalName = "user@externalid.onmicrosoft.com"
// または（クリーニング後にローカル パートが空になった場合）
user.UserPrincipalName = "28687c60@externalid.onmicrosoft.com"
```

**なぜローカル パートを保持するのか？**

ローカル パート（@ より前の識別子）が保持されるのは、**JIT Function がこの変換を逆変換**するため：

```csharp
// JIT Function: TransformUpnForB2C() - JitAuthenticationFunction.cs に配置

// 1. External ID がログイン時に UPN を提供
string externalIdUpn = "user@externalid.onmicrosoft.com";

// 2. JIT がローカル パートを抽出
string localPart = "user";  // @ より前のすべて

// 3. B2C ドメインで B2C UPN を再構築
string b2cUpn = "user@b2cprod.onmicrosoft.com";

// 4. この B2C UPN を使用して B2C ROPC に対して資格情報を検証
```

**主要ポイント**：
- ✅ **ローカル パート保持**: 両方のテナント間で一意の識別子として機能
- ✅ **ドメインのみ変更**: B2C ドメインから External ID ドメイン（インポート）およびその逆（JIT）
- ✅ **双方向マッピング**: インポートは B2C→External ID を変換、JIT は External ID→B2C を変換
- ⚠️ **JIT に重要**: ローカル パートが保持されていない場合、JIT はユーザーを B2C にマップできない

**構成**: ターゲット ドメインは appsettings.json の `Migration.ExternalId.TenantDomain` から取得されます。

#### 2. 認証方法の処理（メール ID）

**コードの場所**: `ImportOrchestrator.cs:EnsureEmailIdentity()`

**重要**: External ID は、認証（Email+Password または Email OTP）のためにすべてのユーザーがメール ID を持つ必要があります。インポート ロジックは、すべてのユーザーがメール ID を取得することを保証します。

```csharp
// 決定木:
// 1. ユーザーが既に emailAddress ID を持っているか確認 -> それを使用（変更なし）
// 2. ユーザーが 'mail' フィールドを持っている場合 -> mail からメール ID を作成
// 3. ユーザーが 'mail' を持っていない場合 -> userPrincipalName をメールとしてフォールバック（userName + userPrincipalName のみを持つユーザー用）

// 結果例:

// シナリオ 1: ユーザーが mail フィールドを持っている
// B2C ユーザー:
{
  "mail": "john.doe@example.com",
  "identities": [
    { "signInType": "userName", "issuerAssignedId": "johndoe" },
    { "signInType": "userPrincipalName", "issuerAssignedId": "guid@b2c.onmicrosoft.com" }
  ]
}
// External ID 結果（JIT を使用した Email+Password）:
{
  "mail": "john.doe@example.com",
  "identities": [
    { "signInType": "userName", "issuerAssignedId": "johndoe", "issuer": "eeid.onmicrosoft.com" },
    { "signInType": "emailAddress", "issuerAssignedId": "john.doe@example.com", "issuer": "eeid.onmicrosoft.com" },
    { "signInType": "userPrincipalName", "issuerAssignedId": "guid@eeid.onmicrosoft.com", "issuer": "eeid.onmicrosoft.com" }
  ]
}

// シナリオ 2: ユーザーが mail フィールドを持っていない（userName + userPrincipalName のみ）
// B2C ユーザー:
{
  "mail": null,
  "identities": [
    { "signInType": "userName", "issuerAssignedId": "loadtest5017" },
    { "signInType": "userPrincipalName", "issuerAssignedId": "a3f2d8e1@b2c.onmicrosoft.com" }
  ]
}
// External ID 結果（userPrincipalName をメール フォールバックとして使用）:
{
  "mail": null,
  "identities": [
    { "signInType": "userName", "issuerAssignedId": "loadtest5017", "issuer": "eeid.onmicrosoft.com" },
    { "signInType": "emailAddress", "issuerAssignedId": "a3f2d8e1@eeid.onmicrosoft.com", "issuer": "eeid.onmicrosoft.com" },
    { "signInType": "userPrincipalName", "issuerAssignedId": "a3f2d8e1@eeid.onmicrosoft.com", "issuer": "eeid.onmicrosoft.com" }
  ]
}
// 警告がログに記録: "User X has no email in 'mail' field. Using userPrincipalName as email fallback."

// シナリオ 3: ユーザーが既に B2C から emailAddress ID を持っている（保持）
// B2C ユーザー:
{
  "mail": "jane@example.com",
  "identities": [
    { "signInType": "emailAddress", "issuerAssignedId": "jane@example.com" },
    { "signInType": "userPrincipalName", "issuerAssignedId": "guid@b2c.onmicrosoft.com" }
  ]
}
// External ID 結果（emailAddress 保持、重複なし）:
{
  "mail": "jane@example.com",
  "identities": [
    { "signInType": "emailAddress", "issuerAssignedId": "jane@example.com", "issuer": "eeid.onmicrosoft.com" },
    { "signInType": "userPrincipalName", "issuerAssignedId": "guid@eeid.onmicrosoft.com", "issuer": "eeid.onmicrosoft.com" }
  ]
}
```

**Email OTP（パスワードレス）構成**:

Email+Password の代わりに Email OTP を使用するには `Migration.Import.MigrationAttributes.UseEmailOtp = true` を設定：

```json
{
  "Migration": {
    "Import": {
      "MigrationAttributes": {
        "UseEmailOtp": true  // emailAddress の代わりにフェデレーション ID（issuer="mail"）を作成
      }
    }
  }
}
```

`UseEmailOtp = true` の場合：
- `signInType = "federated"` と `issuer = "mail"`（Email OTP / パスワードレス）を作成
- ユーザーはメールに送信された OTP でログイン（パスワード移行不要）
- JIT パスワード移行は使用されない（移行するパスワードがない）

`UseEmailOtp = false`（デフォルト）の場合：
- `signInType = "emailAddress"`（Email+Password）を作成
- ユーザーはメールとパスワードでログイン
- JIT パスワード移行が初回ログイン時にパスワードを検証

**ID 保持ルール**:

インポート オーケストレーターはすべての B2C ID タイプを保持：

1. ✅ **userName** ID は保持される（変換されない）
   - B2C からの元の userName が維持される
   - 発行者ドメインのみ External ID ドメインに更新
   - ユーザーは元の userName でログイン可能

2. ✅ **userPrincipalName** ID は保持される（変換されない）
   - 元の userPrincipalName 構造が維持される
   - `TransformUpnForExternalId()` 経由でドメインのみ更新
   - GUID ベースのユーザー名は userPrincipalName のまま（userName に変換されない）

3. ✅ **emailAddress** ID は欠落している場合に追加される
   - ユーザーが 'mail' フィールドを持っている場合 → そのメールを使用
   - ユーザーが 'mail' を持っていない場合 → userPrincipalName をメールとして使用
   - 既存の emailAddress ID は保持される（重複なし）


#### 3. ID 発行者更新

すべての既存 ID 発行者は B2C ドメインから External ID ドメインに更新：

```csharp
// 前
identity.Issuer = "b2cprod.onmicrosoft.com"

// 後
identity.Issuer = "externalid.onmicrosoft.com"
```

### 属性マッピングへの影響

**UPN と認証方法**は属性マッピング構成の対象外：
- UPN 変換は `AttributeMappings` に関係なく自動的に発生
- メール ID 作成ロジックは無効にできない
- SMS（mobilePhone）は存在する場合に自動的に移行
- 標準 ID 変換は無効にできない

**標準 User フィールド**は自動的に移行される（マッピング不要）：
- `mobilePhone` - **SMS ベースの SSPR に重要**
- `mail` - 存在する場合にメール ID に使用
- `displayName`、`givenName`、`surname`
- `streetAddress`、`city`、`state`、`postalCode`、`country`
- `userPrincipalName`、`identities`、`accountEnabled`

**カスタム拡張属性**はマッピングの対象：
- 拡張属性の名前変更に `AttributeMappings` を使用
- 特定の属性のコピーを防ぐために `ExcludeFields` を使用

### UPN/Email/SMS 変換のデバッグ

詳細なログを有効にして変換の詳細を確認：

```json
{
  "Migration": {
    "VerboseLogging": true
  }
}
```

## インポート監査ログ

### 概要

インポート プロセスは、Azure Blob Storage に詳細な監査ログを自動的に作成します。これらのログは、成功/失敗ステータス、タイムスタンプ、ユーザーの詳細を含む各ユーザー移行の証拠を提供します。

### メリット

- **コンプライアンス**: すべての移行活動の永続的な記録
- **監査**: どのユーザーがいつ移行されたかを正確に追跡
- **トラブルシューティング**: エラーの詳細で失敗したインポートを特定
- **レポート**: 移行レポートと統計を生成

### 監査ログ構造

各バッチ インポートは、JSON 形式で別個の監査ログ ファイルを作成：

#### ファイル名形式
```
import-audit_{sourceFile}_batch{number}_{timestamp}.json
```

例：
```
import-audit_000042_batch000_20250111183045.json
```

#### JSON 構造

```json
{
  "Timestamp": "2025-01-11T18:30:45.123Z",
  "SourceBlobName": "users_000042.json",
  "BatchNumber": 0,
  "TotalUsers": 100,
  "SuccessCount": 100,
  "FailureCount": 0,
  "DurationMs": 1234.56,
  "SuccessfulUsers": [
    {
      "B2CObjectId": "12345678-1234-1234-1234-123456789012",
      "ExternalIdObjectId": "87654321-4321-4321-4321-210987654321",
      "UserPrincipalName": "user@externalid.onmicrosoft.com",
      "DisplayName": "John Doe",
      "ImportedAt": "2025-01-11T18:30:45.789Z"
    },
    ...
  ],
  "FailedUsers": [
    {
      "B2CObjectId": "99999999-9999-9999-9999-999999999999",
      "UserPrincipalName": "failed.user@example.com",
      "ErrorMessage": "User already exists",
      "ErrorCode": "Request_ResourceExists",
      "FailedAt": "2025-01-11T18:30:46.123Z"
    },
    ...
  ]
}
```

### 構成

#### ストレージ コンテナー

監査ログは専用の Blob コンテナーに保存：

**デフォルト**: `import-audit`

`appsettings.json` で構成：

```json
{
  "Migration": {
    "Storage": {
      "ImportAuditContainerName": "import-audit"
    }
  }
}
```

#### 自動作成

インポート プロセスは自動的に：
1. 存在しない場合は `import-audit` コンテナーを作成
2. 処理されたバッチごとに 1 つの監査ログを生成
3. 監査ログの保存に失敗してもインポートを継続（警告をログ）

### 監査ログの表示

#### Azure Portal

1. Storage Account に移動
2. **コンテナー**に移動
3. `import-audit` コンテナーを開く
4. 表示する監査ログ ファイルをダウンロード

#### Azure Storage Explorer

1. ストレージ アカウントに接続
2. **Blob コンテナー**を展開
3. `import-audit` を開く
4. 監査ログを参照してダウンロード

#### コマンド ライン（Azure CLI）

すべての監査ログをリスト：
```bash
az storage blob list \
  --account-name <storage-account> \
  --container-name import-audit \
  --output table
```

特定の監査ログをダウンロード：
```bash
az storage blob download \
  --account-name <storage-account> \
  --container-name import-audit \
  --name import-audit_000042_batch000_20250111183045.json \
  --file audit.json
```

#### ローカル開発（Azurite）

Azure Storage Explorer または任意の Blob Storage ツールを使用して接続：
- **接続文字列**: `UseDevelopmentStorage=true`
- **コンテナー**: `import-audit`

### 監査ログ分析

#### 合計移行数をカウント

```bash
# すべての監査ログをダウンロードして成功したインポートの合計をカウント
jq -r '.SuccessCount' *.json | awk '{sum+=$1} END {print sum}'
```

#### 失敗したインポートを検索

```bash
# すべての失敗したユーザー インポートをリスト
jq -r '.FailedUsers[] | "\(.UserPrincipalName): \(.ErrorMessage)"' *.json
```

#### 成功率を計算

```bash
# 全体的な成功率を計算
jq -r '[.TotalUsers, .SuccessCount, .FailureCount] | @csv' *.json
```

#### 移行されたすべてのユーザーを抽出

```bash
# 正常に移行されたすべての B2C ObjectId のリストを取得
jq -r '.SuccessfulUsers[].B2CObjectId' *.json > migrated-users.txt
```

### 保持とクリーンアップ

#### 推奨プラクティス

1. **コンプライアンス期間のログを保持**: 規制に応じて通常 1-7 年
2. **古いログをアーカイブ**: 90 日後に Cool/Archive ティアに移動
3. **重要なログをバックアップ**: 災害復旧のために別のストレージにコピー

#### ストレージ ライフサイクル管理

古い監査ログを自動的にアーカイブするライフサイクル ポリシーを作成：

```json
{
  "rules": [
    {
      "enabled": true,
      "name": "ArchiveImportAudits",
      "type": "Lifecycle",
      "definition": {
        "actions": {
          "baseBlob": {
            "tierToCool": {
              "daysAfterModificationGreaterThan": 90
            },
            "tierToArchive": {
              "daysAfterModificationGreaterThan": 365
            }
          }
        },
        "filters": {
          "blobTypes": ["blockBlob"],
          "prefixMatch": ["import-audit/"]
        }
      }
    }
  ]
}
```

### 監査ログのトラブルシューティング

#### 監査ログが作成されない

**問題**: `import-audit` コンテナーにファイルがない

**解決策**:
1. コンテナーが存在することを確認（自動作成されるが権限を確認）
2. 詳細なログを有効にして監査保存操作を確認
3. 監査保存失敗に関する警告のログを確認
4. ストレージ アカウントに書き込み権限があることを確認

#### 大きな監査ファイル

**問題**: 監査ファイルが非常に大きい

**説明**: 各バッチには 100 以上のユーザーが含まれる可能性があります。大規模な移行の場合：
- 100 ユーザー/バッチ × 100 フィールド/ユーザー = 大きな JSON ファイル
- これは予想通りで正常

**最適化**:
- 長期保存には圧縮（gzip）を検討
- コスト効率のために Blob Storage ティアリングを使用

#### 失敗したユーザーの詳細がない

**問題**: 失敗があっても `FailedUsers` 配列が空

**説明**: 現在の実装はバッチ レベルの失敗を追跡します。バッチ内の個々のユーザー失敗には、Graph API バッチ クライアントの拡張が必要です。

**回避策**: インポート中の詳細なエラー メッセージについてはコンソール ログを確認。

### 監査ログのセキュリティ考慮事項

#### 機密データ

監査ログには以下が含まれます：
- ✅ ユーザー プリンシパル名 (UPN)
- ✅ 表示名
- ✅ ObjectId
- ❌ パスワード（ログされない）
- ❌ 拡張属性値（含まれない）

#### アクセス制御

監査ログへのアクセスを制限：
1. **RBAC**: 権限のある人員のみに `Storage Blob Data Reader` ロールを割り当て
2. **プライベート エンドポイント**: ストレージ アカウントにプライベート エンドポイントを使用
3. **SAS トークン**: 一時アクセス用に期限付き SAS トークンを生成
4. **暗号化**: 保存時の暗号化を有効にする（Azure のデフォルト）


### 例: 移行レポートを生成

サマリー レポートを生成する PowerShell スクリプト：

```powershell
# すべての監査ログをダウンロード
$auditLogs = Get-AzStorageBlob -Container "import-audit" -Context $ctx |
    Get-AzStorageBlobContent -Force

# 解析してサマリー
$summary = $auditLogs | ForEach-Object {
    $content = Get-Content $_.Name | ConvertFrom-Json
    [PSCustomObject]@{
        Timestamp = $content.Timestamp
        SourceFile = $content.SourceBlobName
        Success = $content.SuccessCount
        Failed = $content.FailureCount
        Duration = $content.DurationMs
    }
}

# レポートを表示
$summary | Format-Table -AutoSize
$summary | Export-Csv -Path "migration-report.csv" -NoTypeInformation

# 合計を計算
$totalSuccess = ($summary | Measure-Object -Property Success -Sum).Sum
$totalFailed = ($summary | Measure-Object -Property Failed -Sum).Sum
$avgDuration = ($summary | Measure-Object -Property Duration -Average).Average

Write-Host "`n移行サマリー"
Write-Host "================="
Write-Host "成功合計: $totalSuccess"
Write-Host "失敗合計: $totalFailed"
Write-Host "成功率: $([math]::Round($totalSuccess/($totalSuccess+$totalFailed)*100, 2))%"
Write-Host "平均バッチ期間: $([math]::Round($avgDuration, 2)) ms"
```

## デプロイメント ガイド

### インフラストラクチャ デプロイメント

1. **Azure リソースをデプロイ**
   ```bash
   # Azure Portal または Bicep 経由でデプロイ
   az deployment group create \
     --resource-group rg-b2c-migration \
     --template-file infra/main.bicep
   ```

2. **プライベート エンドポイントを構成**（v2.0 で計画）
   - Storage Account
   - （オプション）Function App

3. **マネージド ID をセットアップ**
   ```bash
   # Function でシステム割り当て ID を有効化
   az functionapp identity assign \
     --name func-b2c-migration \
     --resource-group rg-b2c-migration

   # 権限を付与
   az role assignment create \
     --assignee <managed-identity-id> \
     --role "Storage Blob Data Contributor" \
     --scope <storage-account-resource-id>
   ```

### Function デプロイメント

```bash
cd src/B2CMigrationKit.Function

# ローカルで発行
dotnet publish -c Release

# Azure にデプロイ
func azure functionapp publish func-b2c-migration

# Function を再起動（重要！）
az functionapp restart \
  --name func-b2c-migration \
  --resource-group rg-b2c-migration
```

**重要**: 新しいバイナリを読み込むために、デプロイ後は常に Function App を再起動してください。

### 構成デプロイメント

```bash
# アプリケーション設定を設定
az functionapp config appsettings set \
  --name func-b2c-migration \
  --resource-group rg-b2c-migration \
  --settings \
    "Migration__B2C__TenantId=your-tenant-id" \
    "Migration__ExternalId__TenantId=your-tenant-id"
```

## 運用とモニタリング

### Application Insights ダッシュボード

**移行進捗ダッシュボード**
```kql
let startTime = ago(24h);
traces
| where timestamp > startTime
| where message contains "RUN SUMMARY"
| extend Operation = extract("([A-Z][a-z]+ [A-Z][a-z]+)", 1, message)
| extend TotalItems = toint(extract("Total: ([0-9]+)", 1, message))
| extend SuccessCount = toint(extract("Success: ([0-9]+)", 1, message))
| extend FailureCount = toint(extract("Failed: ([0-9]+)", 1, message))
| project timestamp, Operation, TotalItems, SuccessCount, FailureCount
```

**JIT 移行追跡**
```kql
customMetrics
| where name == "JIT.MigrationsCompleted"
| summarize MigrationsCompleted = sum(value) by bin(timestamp, 1h)
| render timechart
```

**スロットリング分析**
```kql
traces
| where message contains "throttle" or message contains "429"
| summarize ThrottleCount = count() by bin(timestamp, 5m), severity = severityLevel
| render timechart
```

### アラート構成

**推奨アラート：**

1. **高失敗率**
   ```kql
   traces
   | where message contains "failed" or severityLevel >= 3
   | summarize FailureCount = count() by bin(timestamp, 5m)
   | where FailureCount > 10
   ```

2. **過度のスロットリング**
   ```kql
   traces
   | where message contains "429"
   | summarize ThrottleCount = count() by bin(timestamp, 5m)
   | where ThrottleCount > 50
   ```

3. **JIT 認証失敗**
   ```kql
   customMetrics
   | where name == "JIT.CredentialValidationFailed"
   | summarize Failures = sum(value) by bin(timestamp, 5m)
   | where Failures > 20
   ```

### パフォーマンス チューニング

**スループット最適化：**

1. **複数インスタンスで水平スケール**
   - 異なる IP を持つ複数のコンテナー/VM をデプロイ
   - 各インスタンスは専用のアプリ登録を使用
   - IP ベースのスロットリング制限を回避

2. **バッチ サイズを調整**
   - 大きなバッチ = より少ない API 呼び出し
   - 小さなバッチ = より良いエラー分離
   - 推奨: バッチあたり 50-100 ユーザー

3. **遅延を追加**
   - 操作間の間隔を空けるために `BatchDelayMs` を使用
   - バースト スロットリングを削減
   - 全体的なランタイムは増加するが信頼性が向上

### スケーリング パターン

移行ツールキットのスケール方法を理解することは、Microsoft Graph API レート制限を尊重しながら最大スループットを達成するために重要です。

#### Graph API スロットリングの基礎

Microsoft Graph API スロットリングは**2 つの次元**で動作します：

1. **アプリ登録あたり（クライアント ID）** - アプリあたり約 60 操作/秒
2. **IP アドレスあたり** - その IP からのすべてのアプリにわたる累積制限

つまり：
- ✅ 1 アプリで単一インスタンス（1 IP） = 約 60 ops/sec
- ❌ 3 アプリで単一インスタンス（1 IP） ≠ 180 ops/sec（IP により依然として制限される）
- ✅ 各 1 アプリで 3 インスタンス（3 つの異なる IP） = 約 180 ops/sec

**主要原則**: 各インスタンス（Console App または Azure Function）は **1 つのアプリ登録**を使用します。スケールするには、**異なる IP アドレス**に**複数のインスタンス**をデプロイします。

#### Console App スケーリング

**単一インスタンス（デフォルト）**

```json
{
  "Migration": {
    "B2C": {
      "AppRegistration": {
        "ClientId": "app-1",
        "ClientSecretName": "Secret1",
        "Enabled": true
      }
    },
    "ExternalId": {
      "AppRegistration": {
        "ClientId": "app-1",
        "ClientSecretName": "Secret1",
        "Enabled": true
      }
    },
    "BatchSize": 100
  }
}
```

- **スループット**: 約 60 ops/sec
- **ユース ケース**: 小〜中規模の移行（10 万ユーザー未満）
- **利点**: シンプルなセットアップ、低複雑性

**複数インスタンス（コンテナー/VM）**

大規模な移行の場合、異なる IP に複数のインスタンスをデプロイ：

```bash
# コンテナー 1 - IP: 10.0.1.10
docker run -e CONFIG_FILE=appsettings.app1.json migration-console

# コンテナー 2 - IP: 10.0.1.11
docker run -e CONFIG_FILE=appsettings.app2.json migration-console

# コンテナー 3 - IP: 10.0.1.12
docker run -e CONFIG_FILE=appsettings.app3.json migration-console
```

各構成ファイルには**単一の専用アプリ登録**があります：

**appsettings.app1.json**:
```json
{
  "Migration": {
    "ExternalId": {
      "AppRegistration": {
        "ClientId": "app-1",
        "ClientSecretName": "Secret1",
        "Enabled": true
      }
    }
  }
}
```

**複数インスタンスの利点：**
- 真のプロセスと IP の分離
- 独立した障害ドメイン
- 各インスタンスが IP スロットリングをバイパス
- 異なるマシン/コンテナーで実行可能
- **スループット**: N インスタンス × 60 ops/sec = 合計 N×60 ops/sec

**推奨される使用法：**
- 単一インスタンス: 最大 10 万ユーザーの操作
- 複数インスタンス: 10 万ユーザー超の操作または時間に敏感なカットオーバー


**ベスト プラクティス：**
1. 単一インスタンス アプローチから開始
2. スロットリング メトリクスについて Application Insights を監視
3. 必要に応じて水平スケール（異なる IP を持つより多くのインスタンス）
4. 一括操作には、コンテナー内の複数のコンソール インスタンスを使用
5. JIT 操作には、Azure Functions に自動スケーリングを任せる
6. 極端なスケール シナリオ（1 万以上の同時ログイン）の場合のみ、複数の Function App をデプロイ

## セキュリティ ベスト プラクティス

### シークレット管理

1. シークレットをソース管理に**コミットしない**
2. 開発シークレットには**ローカル構成ファイル**（gitignore）を使用
3. 定期的にシークレットを**ローテーション**
4. dev/test/prod に**別々のシークレット**を使用
5. **将来**: Azure Key Vault を使用したセキュアなシークレット管理が v2.0 に含まれる

### ネットワーク セキュリティ

1. 本番環境には**プライベート エンドポイントのみ**（v2.0 で計画）
2. Functions には **VNet 統合**（v2.0 で計画）
3. トラフィックを制限する **NSG ルール**
4. Storage で**パブリック アクセスを無効化**

### 認証

1. サービス プリンシパルより**マネージド ID を優先**
2. クライアント シークレットが必要な場合は**証明書ベースの認証を使用**
3. 権限を最小限に**制限**
4. 定期的に**監査ログをレビュー**

### データ保護

1. **保存時のデータを暗号化**（Azure Storage でデフォルトで有効）
2. すべての通信に **HTTPS のみ**を使用
3. パスワードや機密データを**ログに記録しない**
4. 移行後に**エクスポート ファイルをクリーンアップ**

## トラブルシューティング

### 一般的なエラー

**エラー: "Directory.Read.All permission required"**
- 解決策: アプリ登録で権限を付与し、管理者の同意を行う

**エラー: "Throttle limit exceeded (HTTP 429)"**
- 解決策: バッチ サイズを減らすか、バッチ間に遅延を追加

**エラー: "User already exists"**
- 解決策: 重複ユーザーを確認し、`B2CObjectId` を使用して相関

**エラー: "Password does not meet complexity requirements"**
- 解決策: `PasswordPolicy` 設定と B2C パスワード要件をレビュー

### デバッグのヒント

1. `--verbose` フラグで**詳細なログを有効化**
2. 詳細なエラー トレースについて **Application Insights を確認**
3. 完全な移行前に**小さなサブセットでテスト**
4. ローカル デバッグには Visual Studio/VS Code で**ブレークポイントを使用**
5. テレメトリで **Graph API レスポンスを確認**

### サポート リソース

- Microsoft Graph API ドキュメント: https://docs.microsoft.com/graph
- Azure AD B2C ドキュメント: https://docs.microsoft.com/azure/active-directory-b2c
- Entra External ID ドキュメント: https://docs.microsoft.com/entra/external-id

---

追加のサポートについては、Microsoft 担当者に相談するか、[operations runbook](OPERATIONS.md) を確認してください。
