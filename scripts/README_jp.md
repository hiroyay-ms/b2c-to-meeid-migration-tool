[English](README.md) | **日本語**

# B2C Migration Kit - スクリプト

このディレクトリには、ローカル開発、テスト、および JIT 移行セットアップ用の PowerShell スクリプトが含まれています。

**📖 完全なセットアップ手順については、[開発者ガイド](../docs/DEVELOPER_GUIDE_jp.md)を参照してください**

## 目次

- [前提条件](#前提条件)
- [クイックスタート](#クイックスタート)
- [エクスポート & インポート スクリプト](#エクスポート--インポート-スクリプト)
- [JIT 移行セットアップ](#jit-移行セットアップ)
  - [RSA キーの生成](#1-rsa-キーの生成)
  - [External ID の構成](#2-external-id-の構成)
  - [環境の切り替え](#3-環境の切り替え)
- [構成](#構成)
- [トラブルシューティング](#トラブルシューティング)

---

## 前提条件

### エクスポート/インポート操作用

1. **.NET 8.0 SDK** - コンソールアプリケーションのビルドと実行
   ```powershell
   dotnet --version  # 8.0 以上であること
   ```

2. **Azurite** - ローカル開発用の Azure Storage エミュレーター
   ```powershell
   npm install -g azurite
   ```

3. **構成** - テナント資格情報を含む `appsettings.local.json`
   - [開発者ガイド - 構成](../docs/DEVELOPER_GUIDE_jp.md#configuration-guide)を参照

### JIT 移行テスト用

4. **PowerShell 7.0 以上** - モダンな PowerShell 機能
   ```powershell
   $PSVersionTable.PSVersion  # 7.0 以上であること
   ```

5. **ngrok** - ローカル関数をインターネットに公開
   ```powershell
   choco install ngrok
   # または https://ngrok.com/download からダウンロード
   ```

6. **Azure Function Core Tools** - 関数をローカルで実行
   ```powershell
   npm install -g azure-functions-core-tools@4
   ```

---

---

## クイックスタート

**推奨ワークフロー:** Azurite のセットアップを自動的に処理する PowerShell スクリプトを使用してください。

### B2C からユーザーをエクスポート
```powershell
.\scripts\Start-LocalExport.ps1
```

### External ID にユーザーをインポート
```powershell
.\scripts\Start-LocalImport.ps1
```

**✅ これらのスクリプトが自動的に行うこと:**
- Azurite がインストールされているか確認（インストールされていない場合はプロンプト表示）
- 接続文字列に基づいて Azurite が必要かどうかを自動検出
- 必要に応じて Azurite を自動的に起動（または既存のインスタンスを使用）
- 必要なストレージコンテナーとキューを作成
- コンソールアプリケーションをビルドして実行
- 色分けされた進行状況とステータスメッセージを表示

---

## エクスポート & インポート スクリプト

### Start-LocalExport.ps1

Azure AD B2C からローカル Azurite ストレージにユーザーをエクスポートします。

**使用方法:**
```powershell
.\Start-LocalExport.ps1 [-VerboseLogging] [-ConfigFile "config.json"]
```

**パラメーター:**
- `-ConfigFile` - 構成ファイルのパス（デフォルト: `appsettings.local.json`）
- `-VerboseLogging` - 詳細ログを有効化
- `-SkipAzurite` - Azurite の初期化をスキップ（クラウドストレージを使用）

**動作内容:**
1. 構成ファイルの存在を検証
2. ローカルストレージエミュレーターが必要かどうかを自動検出
3. Azurite がインストールされているか確認（インストールされていない場合はプロンプト表示）
4. 必要に応じて Azurite を起動
5. ストレージコンテナー（`user-exports`、`migration-errors`）を作成
6. コンソールアプリケーションをビルド
7. エクスポート操作を実行

### Start-LocalImport.ps1

ローカル Azurite ストレージから Entra External ID にユーザーをインポートします。

**使用方法:**
```powershell
.\Start-LocalImport.ps1 [-VerboseLogging] [-ConfigFile "config.json"]
```

**パラメーター:**
- `-ConfigFile` - 構成ファイルのパス（デフォルト: `appsettings.local.json`）
- `-VerboseLogging` - 詳細ログを有効化
- `-SkipAzurite` - Azurite の初期化をスキップ（クラウドストレージを使用）

**動作内容:**
1. 構成ファイルの存在を検証
2. ローカルストレージエミュレーターが必要かどうかを自動検出
3. Azurite がインストールされているか確認（インストールされていない場合はプロンプト表示）
4. 必要に応じて Azurite を起動
5. コンソールアプリケーションをビルド
6. インポート操作を実行

---

## JIT 移行セットアップ

ユーザーの初回ログイン時の Just-In-Time パスワード移行の完全なセットアップ。

### 1. RSA キーの生成

**スクリプト:** `New-LocalJitRsaKeyPair.ps1`

ローカルテスト用の RSA-2048 キーペアを生成します（ファイルは `scripts/` ディレクトリに保存されます）。

**使用方法:**
```powershell
.\New-LocalJitRsaKeyPair.ps1
```

**生成されるファイル**（自動的に git-ignored）:
- `jit-private-key.pem` - RSA 秘密鍵（秘密にしてください！）
- `jit-certificate.txt` - X.509 証明書（Azure にアップロード）
- `jit-public-key-x509.txt` - X.509 形式の公開鍵
- `jit-public-key.jwk.json` - JWK 形式の公開鍵

**各ファイルの用途:**

1. **jit-private-key.pem** ⚠️ 機密
   - Azure Function が External ID からのペイロードを復号化するために使用
   - `local.settings.json` → `Migration__JitAuthentication__InlineRsaPrivateKey` に追加
   - このファイルをコミットしたり共有したりしないでください

2. **jit-certificate.txt**
   - base64 形式の X.509 証明書
   - Azure Portal の Custom Extension アプリ登録にアップロード
   - External ID が関数に送信するペイロードを暗号化するために使用

3. **jit-public-key.jwk.json**
   - JSON Web Key 形式の公開鍵
   - `Configure-ExternalIdJit.ps1` スクリプトで使用
   - 共有しても安全（公開鍵です）

**🔐 セキュリティに関する注意:**
- これらのキーは**ローカルテスト専用**です
- 本番環境では、HSM 保護されたキーを持つ Azure Key Vault を使用してください
- 秘密鍵をソースコントロールにコミットしないでください（すでに `.gitignore` に含まれています）

**キーの作成を確認:**
```powershell
Get-ChildItem .\jit-*.* | Select-Object Name, Length

# 予想される出力:
# Name                        Length
# ----                        ------
# jit-private-key.pem          1704
# jit-certificate.txt          1159
# jit-public-key-x509.txt       451
# jit-public-key.jwk.json       394
```

### 2. External ID の構成

**スクリプト:** `Configure-ExternalIdJit.ps1`

デバイスコードフローを使用して、JIT 移行用の External ID 構成を自動化します。

**作成されるもの:**
1. Custom Authentication Extension アプリ登録
2. 暗号化証明書のアップロード
3. Custom Authentication Extension（Azure Function にリンク）
4. テストクライアントアプリケーション（サインインフローのテスト用）
5. サービスプリンシパル（Event Listener に必要）
6. 拡張属性（`RequireMigration` ブール値）
7. Event Listener ポリシー（パスワード送信時に JIT をトリガー）
8. ユーザーフロー（JIT でのサインアップ/サインインを有効化）

**使用方法:**
```powershell
# 基本的な使用方法
.\Configure-ExternalIdJit.ps1 `
    -TenantId "your-external-id-tenant-id" `
    -CertificatePath ".\jit-certificate.txt" `
    -FunctionUrl "https://your-function.azurewebsites.net/api/JitAuthentication" `
    -ExtensionAppObjectId "b2c-extensions-app-object-id" `
    -MigrationAttributeName "RequireMigration"

# ngrok を使用したローカルテスト用
.\Configure-ExternalIdJit.ps1 `
    -TenantId "your-external-id-tenant-id" `
    -CertificatePath ".\jit-certificate.txt" `
    -FunctionUrl "https://your-domain.ngrok-free.dev/api/JitAuthentication" `
    -ExtensionAppObjectId "your-b2c-extensions-app-object-id" `
    -MigrationAttributeName "RequireMigration" `
    -UseUniqueNames
```

**パラメーター:**

| パラメーター | 必須 | 説明 |
|-----------|----------|-------------|
| `TenantId` | はい | External ID テナント ID |
| `CertificatePath` | はい | `jit-certificate.txt` ファイルへのパス |
| `FunctionUrl` | はい | Azure Function エンドポイント URL |
| `ExtensionAppObjectId` | はい | b2c-extensions-app オブジェクト ID |
| `MigrationAttributeName` | いいえ | 拡張属性名（デフォルト: "RequireMigration"） |
| `UseUniqueNames` | いいえ | 一意のアプリ名を生成（複数の構成をテストする場合に便利） |

**b2c-extensions-app オブジェクト ID の見つけ方:**
1. Azure Portal → External ID テナント
2. アプリの登録 → すべてのアプリケーション
3. 検索: `b2c-extensions-app`
4. **オブジェクト ID** をコピー（アプリケーション ID ではありません）

**認証フロー:**
1. スクリプトがデバイスコードログインを開きます（`https://microsoft.com/devicelogin`）
2. External ID 管理者アカウントでサインイン
3. 必要なアクセス許可を付与:
   - `Application.ReadWrite.All`
   - `CustomAuthenticationExtension.ReadWrite.All`
   - `User.Read`

**手動で必要な手順:**
- **ステップ 2:** Azure Portal で Extension App に管理者の同意を付与
  - Portal → アプリの登録 → [Extension App] → API のアクセス許可
  - 「[テナント] に管理者の同意を与える」をクリック
- **ステップ 5:** （オプション）テストクライアントアプリに同意を付与（JIT には不要）

**出力:**
正常に完了すると、スクリプトはすべての ID を含む構成サマリーを表示します:

```
═══════════════════════════════════════════════════════════════
  CONFIGURATION SUMMARY
═══════════════════════════════════════════════════════════════

Custom Extension App:
  → App ID: 00000000-0000-0000-0000-000000000001

Custom Authentication Extension:
  → Extension ID: 00000000-0000-0000-0000-000000000002

Test Client App:
  → App ID: 00000000-0000-0000-0000-000000000003

Event Listener:
  → Migration Property: extension_00000000000000000000000000000001_RequireMigration

User Flow:
  → Display Name: JIT Migration Flow (20251219-123721)
```

**これらの ID を保存**してください（テストとトラブルシューティングに使用）。

### 3. 環境の切り替え

**スクリプト:** `Switch-JitEnvironment.ps1`

Custom Authentication Extension をローカル（ngrok）と Azure Function エンドポイント間で切り替えます。

**使用方法:**
```powershell
# 開発用にローカル ngrok に切り替え
.\Switch-JitEnvironment.ps1 -Environment Local

# 本番用に Azure Function に切り替え
.\Switch-JitEnvironment.ps1 -Environment Azure
```

**パラメーター:**
- `-Environment` - ターゲット環境（`Local` または `Azure`）

**動作内容:**
- Custom Authentication Extension のターゲット URL を更新
- Local の場合: 構成から ngrok URL を使用
- Azure の場合: Azure Function URL を使用
- 切り替え前にエンドポイントが到達可能かどうかを検証

---

---

## 構成

### ローカル開発構成

スクリプトはデフォルトで `appsettings.local.json` を使用し、Azurite 用に事前構成されています:

```json
{
  "Migration": {
    "Storage": {
      "ConnectionStringOrUri": "UseDevelopmentStorage=true",
      "UseManagedIdentity": false
    },
    "KeyVault": null,
    "Telemetry": {
      "Enabled": false
    }
  }
}
```

**これが意味すること:**
- ✅ **ストレージ**: ローカル Azurite エミュレーター（Azure Storage アカウント不要）
- ✅ **シークレット**: 構成で直接 `ClientSecret` を使用（Key Vault 不要）
- ✅ **テレメトリ**: コンソールログのみ（Application Insights 不要）

**ローカルで実行するために必要なもの:**
1. Azurite をインストール: `npm install -g azurite`
2. `appsettings.json` を `appsettings.local.json` にコピー
3. B2C/External ID アプリ登録の資格情報を追加
4. 実行: `.\scripts\Start-LocalExport.ps1`

### 本番/クラウドストレージ

Azurite の代わりに Azure Storage を使用する場合:

```json
{
  "Migration": {
    "Storage": {
      "ConnectionStringOrUri": "https://yourstorage.blob.core.windows.net",
      "UseManagedIdentity": true
    },
    "KeyVault": {
      "VaultUri": "https://yourkeyvault.vault.azure.net/",
      "UseManagedIdentity": true
    }
  }
}
```

スクリプトはこれを自動的に検出し、Azurite をスキップします。

**📖 完全なセットアップ手順については、[開発者ガイド - 構成](../docs/DEVELOPER_GUIDE_jp.md#configuration-guide)を参照してください**

### セキュリティ警告

**実際のシークレットを含む `appsettings.local.json` をソースコントロールにコミットしないでください！**

ファイルはすでに `.gitignore` に含まれています。本番環境では:
- シークレットには Azure Key Vault を使用
- `Migration.KeyVault.VaultUri` を設定
- `ClientSecret` の代わりに `ClientSecretName` を使用
- マネージド ID 認証を有効化

### Azurite ストレージの場所

Azurite はリポジトリルートにデータを保存します。データを表示するには:
- Azure Storage Explorer を使用（ローカルエミュレーターに接続）
- または Azure CLI を使用: `UseDevelopmentStorage=true`

**Azurite の停止:**
```powershell
Stop-Process -Name azurite
```

---

## トラブルシューティング

**「Azurite がインストールされていません」**
```powershell
npm install -g azurite
```

**「構成ファイルが見つかりません」**
- リポジトリルートにいることを確認
- または `-ConfigFile` パラメーターでフルパスを使用

**「Azurite の起動に失敗しました」**
- ポート 10000/10001 が使用中かどうかを確認
- 手動で停止: `Stop-Process -Name azurite`

**「証明書が見つかりません」**（JIT セットアップ）
- パスを確認: `Test-Path ".\jit-certificate.txt"`
- 先に `New-LocalJitRsaKeyPair.ps1` を実行したことを確認

**ビルドエラー**
```powershell
dotnet --version  # 8.0 以上であること
dotnet clean
```

**関数が呼び出されない**（JIT）
- Event Listener の conditions に正しい `appId` があること
- ユーザーフローがテストクライアントアプリに関連付けられていること
- ユーザーに正しい拡張属性が `true` に設定されていること
- ngrok トンネルがアクティブで URL が構成と一致していること

**「B2C 資格情報の検証に失敗しました」**（JIT）
- B2C ROPC アプリが正しく構成されていること
- 同じユーザー名で B2C にユーザーが存在すること
- パスワードが B2C のパスワードと一致すること
- Function 構成に B2C テナント ID とポリシーがあること

### ワークフローの例

完全なローカル開発ワークフロー:

```powershell
# 1. B2C からローカルストレージにエクスポート
.\scripts\Start-LocalExport.ps1 -VerboseLogging

# 2. データを検査（オプション - Azure Storage Explorer を使用）

# 3. External ID にインポート
.\scripts\Start-LocalImport.ps1 -VerboseLogging

# 4. JIT キーを生成
.\scripts\New-LocalJitRsaKeyPair.ps1

# 5. JIT 用に External ID を構成
.\scripts\Configure-ExternalIdJit.ps1 `
    -TenantId "your-tenant-id" `
    -CertificatePath ".\jit-certificate.txt" `
    -FunctionUrl "https://your-ngrok.ngrok-free.dev/api/JitAuthentication" `
    -ExtensionAppObjectId "your-extensions-app-id" `
    -UseUniqueNames

# 6. JIT をテスト（Portal → ユーザーフロー → ユーザーフローを実行）

# 7. 完了したら Azurite を停止
Stop-Process -Name azurite
```

---

## その他のリソース

- **[開発者ガイド](../docs/DEVELOPER_GUIDE_jp.md)** - 完全な開発ドキュメント
- **[Azurite ドキュメント](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)** - ローカルストレージエミュレーター
- **[Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/)** - ストレージデータの検査
- **[ngrok ドキュメント](https://ngrok.com/docs)** - ローカルトンネルのセットアップ
