日本語 | **[English](ARCHITECTURE_GUIDE.md)**

# B2C Migration Kit - アーキテクチャ ガイド

**対象読者**: ソリューション アーキテクト、技術リード、セキュリティ レビュアー、意思決定者

**目的**: このドキュメントは、Azure AD B2C から Microsoft Entra External ID への数百万ユーザーの移行を目的として設計された B2C Migration Kit の包括的なアーキテクチャ概要を提供します。システム コンポーネント、設計原則、スケーラビリティの考慮事項、セキュリティ対策、デプロイメント パターンについて説明します。

---

## 目次

1. [エグゼクティブ サマリー](#1-エグゼクティブ-サマリー)
2. [システム概要](#2-システム概要)
3. [設計原則](#3-設計原則)
4. [コンポーネント アーキテクチャ](#4-コンポーネント-アーキテクチャ)
5. [一括移行コンポーネント](#5-一括移行コンポーネント)
6. [Just-In-Time (JIT) 移行アーキテクチャ](#6-just-in-time-jit-移行アーキテクチャ)
7. [セキュリティ アーキテクチャ](#7-セキュリティ-アーキテクチャ)
8. [スケーラビリティとパフォーマンス](#8-スケーラビリティとパフォーマンス)
9. [デプロイメント トポロジ](#9-デプロイメント-トポロジ)
10. [運用上の考慮事項](#10-運用上の考慮事項)

---

## 1. エグゼクティブ サマリー

> **⚠️ 重要**: このドキュメントは、本番環境向けの移行ソリューションの**目標アーキテクチャ**を説明しています。現在のリリース (v1.0) は、ローカル開発シナリオで検証された**サンプル/プレビュー実装**です。完全な SFI 準拠、自動インフラストラクチャ デプロイメント、Key Vault 統合を含む本番環境機能は、**設計パターンとしてここに文書化されています**が、将来のリリースで完全に実装およびテストされる予定です。

### この移行キットとは？

**B2C Migration Kit** は、**Azure AD B2C** から **Microsoft Entra External ID**（旧 Azure AD for Customers）へのユーザー ID 移行のためのサンプル ソリューションです。現在サポートしている機能：

- **一括エクスポート/インポート**: 並列処理によるユーザー移行（ローカルで 181K 以上のユーザーで検証済み）
- **Just-In-Time (JIT) パスワード移行**: 初回ログイン時のシームレスなパスワード検証（カスタム認証拡張機能でテスト済み）

### 将来追加される機能

- **非同期プロファイル同期**: 共存期間中のユーザー プロファイル同期
- **エンタープライズ セキュリティ アーキテクチャ**: プライベート エンドポイント、マネージド ID、Key Vault 統合を備えた SFI 準拠の設計パターン - アーキテクチャは SFI に対応していますが、このサンプルではまだ実装されていません。


### なぜこのキットを使用するのか？

- **実証済みのアプローチ**: B2C から External ID への移行の検証済み移行パターン
- **スケール対応設計**: マルチインスタンス並列化による 100 万ユーザー以上向けに設計されたアーキテクチャ
- **ゼロ ダウンタイム**: 初回ログイン時にユーザーを透過的に移行（強制パスワード リセットなし）
- **ローカル開発**: クラウド リソースなしでテストと検証を行える完全機能サンプル

### このキットを使用するタイミング

| シナリオ | 推奨事項 |
|----------|----------------|
| B2C から External ID への移行 | ✅ 主要なユース ケース |
| ローカル開発とテスト | ✅ 完全に検証済み |
| 概念実証（20 万ユーザー未満） | ✅ 181K ユーザーでサンプル テスト済み |
| SFI 要件のある本番環境 | ⚠️ 将来のリリースを待つか、セキュリティ強化を実装 |
| 100 万ユーザー超 | ⚠️ アーキテクチャ ガイダンスを使用、スケーリングが必要 |

---

## 2. システム概要

### 高レベル アーキテクチャ

```
┌─────────────────────────────────────────────────────────────────┐
│                    Azure サブスクリプション                      │
│                                                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Console App  │  │Azure Function│  │Azure Function│          │
│  │(Export/Import│  │(JIT Auth)    │  │(Profile Sync)│          │
│  │              │  │              │  │  *将来*      │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
│         │                 │                 │                   │
│         │                 │                 │                   │
│  ┌──────▼─────────────────▼─────────────────▼───────┐          │
│  │           共有コア ライブラリ                     │          │
│  │  (Services, Models, Orchestrators, Abstractions)  │          │
│  └──────┬───────────┬──────────┬──────────┬──────────┘          │
│         │           │          │          │                     │
│         ▼           ▼          ▼          ▼                     │
│  ┌──────────┐ ┌─────────┐ ┌─────────┐ ┌──────────┐            │
│  │ Blob     │ │Key Vault│ │App      │ │ Storage  │            │
│  │ Storage  │ │         │ │ Insights│ │ Queue    │            │
│  │          │ │*将来    │ │         │ │ *将来*   │            │
│  └──────────┘ └─────────┘ └─────────┘ └──────────┘            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
         │                           │
         │                           │
         ▼                           ▼
┌─────────────────┐         ┌─────────────────┐
│ Azure AD B2C    │         │ External ID     │
│ (ソース テナント)│         │ (ターゲット テナント) │
└─────────────────┘         └─────────────────┘
```

### データ フロー

#### フェーズ 1: 一括エクスポート
```
B2C テナント → Graph API → Export Service → JSON ファイル → Blob Storage
```

#### フェーズ 2: 一括インポート
```
Blob Storage → JSON ファイル → Import Service → Graph API → External ID テナント
```

#### フェーズ 3: JIT 移行（初回ログイン）
```
ユーザー ログイン → External ID → カスタム拡張機能 → JIT Function → B2C ROPC 検証
          ↓
External ID がパスワードを設定 + 移行済みをマーク → 認証完了
```

#### フェーズ 4: プロファイル同期（共存期間）- *未実装*
```
プロファイル更新 (B2C または External ID) → Queue → Sync Function → 他方のテナントを更新
```

> **⚠️ 注意**: プロファイル同期はアーキテクチャ設計の一部ですが、現在のバージョンでは実装されていません。このフェーズは、テナント共存中の双方向プロファイル更新をサポートするために、将来のリリースで開発される予定です。

---

## 3. 設計原則

> **🚧 注意**: 以下の原則は**目標とする本番環境アーキテクチャ**を説明しています。現在のリリース (v1.0) は、ローカル開発向けのコア移行機能を実装しています。SFI 準拠機能（プライベート エンドポイント、VNet、Key Vault）は、将来の実装またはカスタム デプロイメントのための設計ガイダンスとしてここに文書化されています。

### 3.1 SFI 準拠のモジュラー アーキテクチャ（目標設計）

1. **共有コア ライブラリ** (`B2CMigrationKit.Core`)
   - すべてのビジネス ロジック、モデル、抽象化
   - Console、Function、および将来のホスティング環境で再利用可能
   - ホスティング固有の依存関係なし

2. **コンソール アプリケーション** (`B2CMigrationKit.Console`)
   - 開発者フレンドリーなローカル実行
   - 詳細なログを備えたリッチな CLI
   - 高速な反復とデバッグ

3. **Azure Functions** (`B2CMigrationKit.Function`)
   - 本番環境グレードのクラウド実行
   - スケーラブルでイベント ドリブンなアーキテクチャ
   - Azure モニタリングとセキュリティとの統合

### 3.2 セキュリティ ファースト

- **プライベート エンドポイントのみ**: すべての Azure PaaS リソース（Storage、Key Vault）はプライベート ネットワーク経由でのみアクセス可能
- **マネージド ID**: コードまたは構成にシークレットなし（Key Vault 参照を除く）
- **すべてで暗号化**: 保存時（Storage/Key Vault）および転送中（HTTPS/TLS 1.2+）
- **最小権限**: 最小限の必要な権限を持つサービス プリンシパル

### 3.3 可観測性

- **構造化ログ**: 名前付きプロパティを持つ Application Insights（文字列連結なし）
- **実行サマリー**: 実行ごとに 1 つの集約ログ（カウント、期間、エラー）
- **分散トレーシング**: コンポーネント間の相関 ID
- **カスタム メトリクス**: 移行の進捗、スロットリング イベント、パフォーマンスを追跡

### 3.4 信頼性

- **べき等性**: 重複なく安全に操作を再試行可能
- **グレースフル デグラデーション**: 重大でない障害でも処理を継続
- **チェックポイント/再開**: エクスポート/インポートは最後の成功バッチから再開可能
- **サーキット ブレーカー**: API スロットリング時の自動バックオフ（HTTP 429）

### 3.5 スケーラビリティ

- **マルチアプリ並列化**: 3-5 個のアプリ登録を使用してスループットを乗算
- **ステートレス設計**: 共有状態なしの水平スケーリング
- **非同期処理**: ノンブロッキング更新のためのキューベースのプロファイル同期 *（将来のリリースで計画）*
- **バッチ処理**: 効率的な Graph API バッチ リクエスト（呼び出しあたり 50-100 ユーザー）

---

## 4. コンポーネント アーキテクチャ

### 4.1 コア ライブラリ構造

```
B2CMigrationKit.Core/
├── Abstractions/
│   ├── IOrchestrator.cs              # マルチステップ ワークフローを調整
│   ├── IGraphClient.cs               # Graph API 操作（ユーザー CRUD）
│   ├── IBlobStorageClient.cs         # エクスポート/インポート ファイル ストレージ
│   ├── IQueueClient.cs               # プロファイル同期メッセージ キュー（将来）
│   ├── IAuthenticationService.cs     # B2C ROPC 検証
│   ├── ISecretProvider.cs            # Key Vault 統合
│   └── ITelemetryService.cs          # カスタム メトリクス/イベント
├── Configuration/
│   ├── MigrationOptions.cs           # ルート構成バインディング
│   ├── B2COptions.cs                 # B2C テナント構成
│   ├── ExternalIdOptions.cs          # External ID 構成
│   ├── JitAuthenticationOptions.cs   # JIT Function 設定
│   ├── StorageOptions.cs             # Blob/Queue 構成
│   └── RetryOptions.cs               # スロットリング/バックオフ設定
├── Models/
│   ├── UserProfile.cs                # 統合ユーザー モデル
│   ├── ExportResult.cs               # エクスポート操作結果
│   ├── ImportResult.cs               # インポート操作結果
│   ├── JitAuthenticationRequest.cs   # External ID からの JIT ペイロード
│   └── JitAuthenticationResponse.cs  # External ID への JIT レスポンス
├── Services/
│   ├── Orchestrators/
│   │   ├── ExportOrchestrator.cs     # 一括エクスポート ワークフロー
│   │   ├── ImportOrchestrator.cs     # 一括インポート ワークフロー
│   │   └── JitMigrationService.cs    # JIT 検証ロジック
│   ├── Graph/
│   │   ├── B2CGraphClient.cs         # B2C 固有の操作
│   │   └── ExternalIdGraphClient.cs  # External ID 操作
│   ├── Storage/
│   │   ├── BlobStorageClient.cs      # Azure Blob 操作
│   │   └── QueueClient.cs            # Azure Queue 操作
│   ├── Authentication/
│   │   ├── AuthenticationService.cs  # B2C ROPC 実装
│   │   └── RsaKeyProvider.cs         # JIT RSA キー管理
│   └── Observability/
│       └── TelemetryService.cs       # Application Insights ラッパー
└── Extensions/
    ├── ServiceCollectionExtensions.cs # DI 登録
    └── RetryPolicyExtensions.cs       # Polly リトライ ポリシー
```

### 4.2 依存性注入パターン

すべてのサービスは、インターフェースベースの抽象化を使用した**コンストラクター注入**を使用します：

```csharp
// 登録 (Console + Function)
services.AddCoreServices(configuration);

// サービス依存関係の例
public class ImportOrchestrator : IOrchestrator<ImportResult>
{
    private readonly IGraphClient _externalIdClient;
    private readonly IBlobStorageClient _blobClient;
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<ImportOrchestrator> _logger;

    public ImportOrchestrator(
        IGraphClient externalIdClient,
        IBlobStorageClient blobClient,
        ITelemetryService telemetry,
        ILogger<ImportOrchestrator> logger)
    {
        // ...
    }
}
```

---

## 5. 一括移行コンポーネント

### 5.1 一括エクスポート アーキテクチャ

**目的**: 一括インポート用に B2C からすべてのユーザー プロファイルを JSON ファイルに抽出。

#### 主な機能

- **バッチ ページネーション**: Graph API 呼び出しあたり 100-1000 ユーザーを取得
- **並列実行**: 異なるアプリ登録を使用した複数のエクスポート スレッド
- **スロットリング制御**: HTTP 429（レート制限超過）時の指数バックオフ
- **再開サポート**: 最後にエクスポートされたページを追跡し、チェックポイントから再開
- **選択フィールド**: ペイロード サイズを最小化するために `$select` を使用

#### プロセス フロー

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. エクスポートの初期化                                          │
│    - 構成を読み込み（B2C テナント、アプリ登録）                   │
│    - バッチ サイズと並列度を決定                                 │
└────────────┬────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 2. B2C ユーザーをクエリ（ページネーション）                       │
│    GET /users?$top=1000&$select=id,displayName,...              │
│    - 継続トークンを処理                                          │
│    - 進捗を追跡（ページ 1 / N）                                  │
└────────────┬────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 3. 標準形式に変換                                                │
│    - userPrincipalName、displayName、emails を抽出              │
│    - 将来の参照用に B2C objectId を保存                          │
│    - 無効な文字をサニタイズ                                      │
└────────────┬────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 4. Blob Storage に書き込み                                       │
│    - ファイル: users_{batchNumber}.json                          │
│    - 各ファイルには 1-10K ユーザーが含まれる                      │
│    - リトライ付きアトミック書き込み                               │
└────────────┬────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 5. 進捗とメトリクスをログ                                        │
│    - "50,000 ユーザーをエクスポート（ページ 50 / 200）"          │
│    - スロットリング イベントを追跡（429 カウント）               │
│    - テレメトリを送信: UsersExportedCount                       │
└─────────────────────────────────────────────────────────────────┘
```

#### マルチアプリ並列化戦略

アプリあたりのレート制限（約 60 読み取り/秒）を克服するために、**2-3 個のアプリ登録**をデプロイ：

```
┌──────────────────┐
│ Export Instance 1│  App Registration 1
│ ページ: 1-100    │  約 60 読み取り/秒
└──────────────────┘
        +
┌──────────────────┐
│ Export Instance 2│  App Registration 2
│ ページ: 101-200  │  約 60 読み取り/秒
└──────────────────┘
        =
  合計約 120 読み取り/秒
```

**構成**:

```json
{
  "Migration": {
    "B2C": {
      "AppRegistration": {
        "ClientId": "app-1-client-id",
        "ClientSecret": "...",
        "Enabled": true
      },
      "BatchSize": 1000,
      "ParallelThreads": 2
    }
  }
}
```

#### セキュリティ対策

- **サービス プリンシパル**: B2C の `Directory.Read.All`（アプリケーション権限）
- **資格情報の保存**: Azure Key Vault のクライアント シークレット
- **ネットワーク**: Blob Storage へのプライベート エンドポイント（パブリック アクセスなし）
- **データ保護**: 保存時に暗号化されたエクスポート ファイル（Azure Storage SSE）

### 5.2 一括インポート アーキテクチャ

**目的**: エクスポートされた JSON ファイルから External ID テナントにすべてのユーザーを作成。

#### 主な機能

- **チャンク読み取り**: ファイル全体をメモリに読み込まずに大きな JSON ファイルをストリーミング
- **バッチ リクエスト**: 単一の Graph API 呼び出しで 50-100 ユーザー作成を結合
- **UPN ドメイン変換**: B2C ドメインを External ID ドメインに置き換え（JIT 中に逆変換）
- **拡張属性**: `B2CObjectId` と `RequireMigration` カスタム属性を設定
- **プレースホルダー パスワード**: ランダムな強力なパスワードを生成（JIT までユーザーはログイン不可）
- **検証**: インポート後のユーザー数検証

#### プロセス フロー

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. Blob Storage からエクスポート ファイルを読み取り              │
│    - すべての users_*.json ファイルをリスト                      │
│    - 順次または並列で処理                                        │
└────────────┬────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 2. JSON を解析してユーザーを準備                                 │
│    - バッチあたり 50-100 ユーザーを読み取り                      │
│    - ランダム パスワードを生成（16 文字、複雑）                  │
│    - forceChangePasswordNextSignIn = true を設定                │
└────────────┬────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 3. External ID 互換性のために UPN を変換                         │
│    - B2C ドメインを External ID ドメインに置き換え               │
│    - 例: user@b2ctenant.onmicrosoft.com →                       │
│               user@externalidtenant.onmicrosoft.com             │
│    - ローカル パートを保持（ユーザー名は同じまま）               │
│    - UserPrincipalName と Identities の両方を更新               │
└────────────┬────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 4. カスタム属性を設定                                            │
│    - extension_{ExtensionAppId}_B2CObjectId = <B2C GUID>        │
│    - extension_{ExtensionAppId}_RequireMigration = true         │
│    （パスワードがまだ移行されていないため true）                 │
└────────────┬────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 5. Graph API 経由でユーザーを作成（バッチ リクエスト）           │
│    POST /v1.0/$batch                                             │
│    {                                                             │
│      "requests": [                                               │
│        { "method": "POST", "url": "/users", "body": {...} },    │
│        ...                                                       │
│      ]                                                           │
│    }                                                             │
└────────────┬────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 6. レスポンスを処理して失敗を再試行                              │
│    - 成功: ユーザー作成をログ                                    │
│    - 失敗: エラーをログ + 指数バックオフで再試行                 │
│    - 手動レビュー用に失敗を収集                                  │
└────────────┬────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 7. インポート後の検証                                            │
│    - External ID をクエリ: 総ユーザー数                          │
│    - B2C エクスポート数と比較                                    │
│    - 差異を報告                                                  │
└─────────────────────────────────────────────────────────────────┘
```

#### マルチアプリ並列化戦略

エクスポートと同様に、スループットを向上させるために **3-5 個のアプリ登録**を使用：

```
┌──────────────────┐
│ Import Instance 1│  App Registration 1
│ ファイル: 1-50   │  約 60 書き込み/秒
└──────────────────┘
        +
┌──────────────────┐
│ Import Instance 2│  App Registration 2
│ ファイル: 51-100 │  約 60 書き込み/秒
└──────────────────┘
        +
┌──────────────────┐
│ Import Instance 3│  App Registration 3
│ ファイル: 101-150│  約 60 書き込み/秒
└──────────────────┘
        =
  合計約 180 書き込み/秒
```

**重要**: 各インスタンスは IP レベルのスロットリングを回避するために**異なる IP アドレス**で実行する必要があります（[セクション 8.2](#82-マルチインスタンス-スケーリング-アーキテクチャ) を参照）。

#### セキュリティ対策

- **サービス プリンシパル**: External ID の `User.ReadWrite.All`（アプリケーション権限）
- **資格情報の保存**: Azure Key Vault のクライアント シークレット
- **ネットワーク**: Blob Storage と Key Vault へのプライベート エンドポイント
- **監査ログ**: すべてのユーザー作成を Application Insights にログ

---

## 6. Just-In-Time (JIT) 移行アーキテクチャ

### 6.1 概要

JIT 移行は、External ID への初回ログイン時に**シームレスなパスワード検証**を可能にし、ユーザーがパスワードをリセットする必要をなくします。

**主要な概念**: ユーザーが初めて External ID にログインするとき：
1. External ID が `RequireMigration` カスタム属性をチェック
2. `RequireMigration = true`（未移行）の場合、カスタム認証拡張機能をトリガー
3. 拡張機能が暗号化されたパスワードとユーザーの UPN で Azure Function を呼び出す
4. Function が **UPN ドメイン変換を逆変換**（External ID ドメイン → B2C ドメイン）
   - External ID UPN: `user@externalid.onmicrosoft.com`
   - ローカル パートを抽出: `user`（インポートから保持）
   - B2C UPN を再構築: `user@b2c.onmicrosoft.com`
5. Function が再構築された B2C UPN を使用して ROPC 経由で B2C に対してパスワードを検証
6. 有効な場合、External ID がパスワードを設定し `RequireMigration = false` をマーク
7. 以降のログインは JIT フローをスキップ（External ID で直接認証）

**重要な UPN フロー**:
```
インポート フェーズ:  B2C UPN (user@b2c.com) → 変換 → External ID UPN (user@externalid.com)
                                                     [ローカル パートを保持: "user"]

JIT フェーズ:     External ID UPN (user@externalid.com) → 逆変換 → B2C UPN (user@b2c.com)
                                                         [同じローカル パートを使用: "user"]
```

### 6.2 アーキテクチャ コンポーネント

```
┌────────────────────────────────────────────────────────────────────┐
│                         External ID テナント                        │
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │ ユーザー サインイン フロー                                   │ │
│  │ 1. ユーザーが UPN + パスワードを送信                         │ │
│  │ 2. RequireMigration 属性をチェック                           │ │
│  │ 3. true の場合 → OnPasswordSubmit リスナーをトリガー         │ │
│  └──────────────────────────┬───────────────────────────────────┘ │
│                             │                                      │
│  ┌──────────────────────────▼───────────────────────────────────┐ │
│  │ カスタム認証拡張機能                                         │ │
│  │ - RSA 公開キーを持つアプリ登録                               │ │
│  │ - パスワード ペイロードを暗号化（JWE 形式）                  │ │
│  │ - Azure Function に POST を送信                              │ │
│  │ - タイムアウト: 最大 2 秒                                    │ │
│  └──────────────────────────┬───────────────────────────────────┘ │
└────────────────────────────┼────────────────────────────────────────┘
                             │ HTTPS（JWE 暗号化ペイロード）
                             ▼
┌────────────────────────────────────────────────────────────────────┐
│                   Azure Function（JIT エンドポイント）              │
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │ 1. パスワードを復号                                          │ │
│  │    - Key Vault から RSA 秘密キーを取得（キャッシュ済み）     │ │
│  │    - JWE ペイロードを復号 → {password, nonce}                │ │
│  │    - nonce が存在することを検証（リプレイ保護）              │ │
│  └──────────────────────────┬───────────────────────────────────┘ │
│                             │                                      │
│  ┌──────────────────────────▼───────────────────────────────────┐ │
│  │ 2. UPN を変換して資格情報を検証（B2C ROPC）                  │ │
│  │    a) UPN 変換を逆変換:                                      │ │
│  │       - 入力: user@externalid.onmicrosoft.com                │ │
│  │       - ローカル パートを抽出: "user"                        │ │
│  │       - 再構築: user@b2c.onmicrosoft.com                     │ │
│  │    b) B2C /oauth2/v2.0/token に POST                         │ │
│  │       - grant_type=password                                  │ │
│  │       - username={再構築された-b2c-upn}                      │ │
│  │       - password={復号されたパスワード}                      │ │
│  │    成功 → トークン受信（有効なパスワード）                   │ │
│  │    失敗 → invalid_grant（間違ったパスワード）                │ │
│  └──────────────────────────┬───────────────────────────────────┘ │
│                             │                                      │
│  ┌──────────────────────────▼───────────────────────────────────┐ │
│  │ 3. External ID にレスポンスを返す                            │ │
│  │    成功:                                                     │ │
│  │    { "actions": [{ "action": "MigratePassword" }] }          │ │
│  │                                                              │ │
│  │    失敗:                                                     │ │
│  │    { "actions": [{ "action": "BlockSignIn" }] }              │ │
│  └──────────────────────────┬───────────────────────────────────┘ │
└────────────────────────────┼────────────────────────────────────────┘
                             │
                             ▼
┌────────────────────────────────────────────────────────────────────┐
│                         External ID テナント                        │
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │ MigratePassword の場合:                                      │ │
│  │ - ユーザーのパスワードを送信された値に設定                   │ │
│  │ - RequireMigration = false を設定（移行済みとしてマーク）    │ │
│  │ - 認証フローを完了                                           │ │
│  │ - アプリケーションにトークンを発行                           │ │
│  └──────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────┘
```

### 6.3 セキュリティ対策

#### 6.3.1 保存時の暗号化

- **秘密キー**: Azure Key Vault にシークレットとして保存（PEM 形式）
  - Function マネージド ID へのアクセス制限（`Get Secret` 権限のみ）
  - Key Vault 監査ログ有効（すべてのアクセスを追跡）
- **パスワード**: ログや保存は決して行わない
  - リクエスト処理中のみメモリに存在
  - 検証直後にクリア

#### 6.3.2 転送中の暗号化

- **External ID → Function**: RSA-2048/4096 公開キーによる JWE（JSON Web Encryption）
  - パスワードは External ID ネットワークを離れる**前に**暗号化
  - Azure Function（一致する秘密キーを持つ）のみが復号可能
- **Function → B2C**: TLS 1.2+ による HTTPS
  - ROPC エンドポイントは OAuth 2.0 セキュア トークン エンドポイントを使用
- **Function → External ID Graph API**: OAuth 2.0 クライアント資格情報フロー
  - 短期間のアクセス トークン（1 時間の有効期間）

#### 6.3.3 認証と承認

**External ID → Function**:
- Azure AD トークン認証
- Function は External ID テナントが発行したベアラー トークンを検証
- トークン オーディエンスはカスタム拡張機能アプリ ID URI と一致する必要がある

**Function → B2C**:
- `Directory.Read.All`（アプリケーション権限）を持つサービス プリンシパル
- クライアント資格情報フロー（Key Vault からの ClientId + ClientSecret）

**Function → External ID**:
- マネージド ID またはサービス プリンシパル
- Graph API 権限: `User.ReadWrite.All`（アプリケーション）

#### 6.3.4 リプレイ保護

- **Nonce**: 暗号化ペイロードに含まれるランダム値
- Function によって検証（存在必須）

#### 6.3.5 タイムアウト保護

- **External ID タイムアウト**: 最大 2 秒（ハード リミット）
  - Function はこのウィンドウ内で応答する必要がある
- **Function 内部タイムアウト**: 1.5 秒（設定可能）
  - 制限を超えると B2C ROPC 呼び出しを中止
  - 部分状態を防ぐために `BlockSignIn` を返す

### 6.4 パフォーマンス最適化

#### 目標メトリクス

- **合計 JIT フロー**: <500ms（2 秒のタイムアウト内に十分収まる）
  - ステップ 1（復号）: <20ms
  - ステップ 2（B2C ROPC）: 200-400ms（ネットワーク + 認証）
  - ステップ 3（複雑性チェック）: <10ms
  - ステップ 4（レスポンス）: <5ms

#### 最適化戦略

**1. RSA キー キャッシュ**
```csharp
private static string? _cachedPrivateKey;
private static readonly SemaphoreSlim _keyLoadLock = new(1, 1);

// 最初のリクエスト: Key Vault から読み込み（約 100ms）
// 以降のリクエスト: キャッシュから取得（約 1ms）
```

**2. 接続プーリング**
- 接続再利用を備えた HttpClient シングルトン
- B2C と External ID Graph API のトークン キャッシュ

**3. リージョン デプロイメント**
- External ID テナントと同じリージョンに Function をデプロイ
- ネットワーク遅延を削減（<50ms）

**4. バックグラウンド処理**
- 監査ログとテレメトリを Fire-and-Forget として処理
- 重要なノンブロッキング タスクには永続キューを使用

---

## 7. セキュリティ アーキテクチャ

> **⚠️ 実装ステータス**: このセクションで説明されているセキュリティ パターンは、**目標とする本番環境アーキテクチャ**を表しています。現在のリリース (v1.0) には以下が含まれます：
> - ✅ **利用可能**: TLS 1.2+、クライアント シークレット認証、コード内にシークレットなし
> - 🔜 **将来のリリース**: Key Vault 統合、マネージド ID、プライベート エンドポイント、VNet 統合、完全な SFI 準拠
>
> **今後の機能 (v2.0+)**:
> - 本番環境 Key Vault 統合
> - プライベート エンドポイント構成
> - 自動インフラストラクチャ デプロイメント（Bicep/Terraform）
> - マネージド ID 実装

### 7.1 ネットワーク セキュリティ（SFI 準拠 - 目標アーキテクチャ）

**目標状態**: すべての Azure PaaS リソースは、パブリック ネットワーク アクセスを無効にしたプライベート エンドポイントのみである必要があります。

**現在の状態**: アーキテクチャとコード パターンが提供されており、本番環境での使用には検証とテストが必要です。

#### ベースライン コントロール

```
┌────────────────────────────────────────────────────────────────┐
│                      仮想ネットワーク (VNet)                    │
│                                                                │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │ App サブネット (10.0.1.0/24)                            │  │
│  │ - Azure Function VNet 統合                              │  │
│  │ - NSG: プライベート エンドポイント サブネットへの       │  │
│  │   アウトバウンドを許可                                  │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                                │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │ プライベート エンドポイント サブネット (10.0.2.0/24)    │  │
│  │ ┌────────────┐  ┌────────────┐  ┌────────────┐          │  │
│  │ │ PE: Blob   │  │ PE: Key    │  │ PE: Queue  │          │  │
│  │ │ Storage    │  │ Vault      │  │            │          │  │
│  │ └────────────┘  └────────────┘  └────────────┘          │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

#### Infrastructure as Code（Bicep 例）

```bicep
// プライベート エンドポイントを持つ Key Vault
resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-migration-prod'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'premium' }
    publicNetworkAccess: 'Disabled'  // SFI 要件
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'None'
    }
  }
}

// Key Vault のプライベート エンドポイント
module kvPe 'modules/privateEndpoint.bicep' = {
  name: 'kvPe'
  params: {
    privateLinkServiceId: kv.id
    groupIds: ['vault']
    subnetId: privateEndpointSubnet.id
  }
}

// プライベート エンドポイントを持つ Storage Account
resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'stmigrationprod'
  location: location
  sku: { name: 'Standard_ZRS' }
  kind: 'StorageV2'
  properties: {
    publicNetworkAccess: 'Disabled'  // SFI 要件
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}
```

### 7.2 ID とアクセス管理

#### マネージド ID 戦略

すべての Azure サービスは**システム割り当てマネージド ID**を使用（シークレットを持つサービス プリンシパルなし）：

```
┌──────────────────┐
│ Azure Function   │
│ (マネージド ID   │
│  オブジェクト ID: xxx) │
└────────┬─────────┘
         │
         │ Azure RBAC
         ▼
┌────────────────────────────────────────────────────────────┐
│ Azure リソース                                             │
│                                                            │
│ ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│ │ Key Vault    │  │ Blob Storage │  │ Queue        │      │
│ │ ロール:      │  │ ロール:      │  │ ロール:      │      │
│ │ Get Secret   │  │ Blob Data    │  │ Queue Data   │      │
│ │              │  │ Contributor  │  │ Contributor  │      │
│ └──────────────┘  └──────────────┘  └──────────────┘      │
└────────────────────────────────────────────────────────────┘
```

#### サービス プリンシパル権限

**B2C アプリ登録**（エクスポートと JIT ROPC 用）：
- `Directory.Read.All`（アプリケーション権限）
- B2C への書き込み権限なし

**External ID アプリ登録**（インポートと JIT 更新用）：
- `User.ReadWrite.All`（アプリケーション権限）
- 特定のユーザー プロパティに制限（グローバル管理者権限なし）

### 7.3 データ保護

#### 保存時の暗号化

- **Blob Storage**: Microsoft マネージド キーによる Azure Storage Service Encryption (SSE)
- **Key Vault**: Hardware Security Module (HSM) バックアップ キー（Premium ティア）
- **Application Insights**: 90 日間の保持期間を持つ暗号化ログ

#### 転送中の暗号化

- **すべての HTTP トラフィック**: TLS 1.2 以上（サポートされている場合は TLS 1.3）
- **証明書検証**: Azure エンドポイントに対する厳格な証明書ピンニング
- **プレーン HTTP なし**: すべての接続で HTTPS を強制

#### シークレット管理

**コードまたは構成ファイルにシークレットなし**：
- すべてのシークレットは Azure Key Vault に保存
- 構成は Key Vault 参照を使用：
  ```json
  {
    "B2C:ClientSecret": "@Microsoft.KeyVault(SecretUri=https://kv-prod.vault.azure.net/secrets/B2CAppSecret/)"
  }
  ```

### 7.4 監査とコンプライアンス

#### 監査ログ

**Key Vault 監査ログ**：
```kql
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.KEYVAULT"
| where OperationName == "SecretGet"
| project TimeGenerated, CallerIPAddress, identity_claim_appid_g, ResultSignature
```

**Function 呼び出しログ**：
```kql
traces
| where operation_Name == "JitAuthentication"
| extend UserId = customDimensions.UserId
| extend Result = customDimensions.Result
| project timestamp, UserId, Result, duration
```

**ユーザー サインイン監査（External ID）**：
- Azure AD 監査ログと統合
- カスタム拡張機能の結果を含む JIT 移行イベントを追跡
- 保持期間: 30 日間（長期保持のために Blob にエクスポート）

---

## 8. スケーラビリティとパフォーマンス

### 8.1 Graph API スロットリング モデル

**重要**: Microsoft Graph API スロットリングは**2 つの次元**で動作します：

1. **アプリ登録あたり（クライアント ID）** - アプリあたり約 60 操作/秒
2. **IP アドレスあたり** - その IP からのすべてのアプリにわたる累積制限
3. **テナントあたり** - テナント内のすべてのアプリで 200 RPS
4. **書き込み操作**（ユーザー作成）はより低いスロットリング制限がある

**影響**：
- ✅ 1 アプリで単一インスタンス = 約 60 ops/sec
- ❌ 3 アプリで単一インスタンス ≠ 180 ops/sec（IP により依然として制限される）
- ✅ 各 1 アプリで 3 インスタンス（異なる IP） = 約 180 ops/sec

### 8.2 マルチインスタンス スケーリング アーキテクチャ

60 ops/sec を超えてスケールするには、**異なる IP アドレス**に**複数のインスタンス**をデプロイ：

```
┌─────────────────┐
│  Container 1    │  App Registration 1
│  IP: 10.0.1.10  │  約 60 ops/sec
└─────────────────┘
         +
┌─────────────────┐
│  Container 2    │  App Registration 2
│  IP: 10.0.1.11  │  約 60 ops/sec
└─────────────────┘
         +
┌─────────────────┐
│  Container 3    │  App Registration 3
│  IP: 10.0.1.12  │  約 60 ops/sec
└─────────────────┘
         =
   合計約 180 ops/sec
```

#### デプロイメント オプション

**オプション 1: Azure Container Instances (ACI)**
```bash
az container create \
  --name migration-import-1 \
  --image migrationkit:latest \
  --vnet my-vnet --subnet subnet-1 \
  --environment-variables APPSETTINGS_PATH=appsettings.app1.json
```

**オプション 2: Azure Kubernetes Service (AKS)**
- 一意の IP アドレスを持つ 3-5 個の Pod をデプロイ
- IP 割り当てのために DaemonSet または StatefulSet を使用
- IP の多様性を確保するためにネットワーク ポリシーを設定

**オプション 3: 仮想マシン**
- 異なるサブネットに 3-5 個の VM をデプロイ
- 各 VM は異なるアプリ登録で Console アプリを実行

### 8.3 スロットリング管理

#### リトライ ポリシー構成

```csharp
// ジッター付き指数バックオフ
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<RateLimitExceededException>()
    .WaitAndRetryAsync(
        retryCount: 5,
        sleepDurationProvider: attempt => 
            TimeSpan.FromSeconds(Math.Pow(2, attempt)) 
            + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
        onRetry: (exception, timespan, attempt, context) =>
        {
            _logger.LogWarning(
                "Throttled (429). Retry {Attempt} after {Delay}ms",
                attempt, timespan.TotalMilliseconds);
        });
```

#### サーキット ブレーカー パターン

```csharp
var circuitBreakerPolicy = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 10,
        durationOfBreak: TimeSpan.FromMinutes(1),
        onBreak: (exception, duration) =>
        {
            _logger.LogError("Circuit breaker opened for {Duration}", duration);
        },
        onReset: () =>
        {
            _logger.LogInformation("Circuit breaker reset");
        });
```

---

## 9. デプロイメント トポロジ

### 9.1 開発環境

```
┌─────────────────────────────────────────────────────────────┐
│  開発者ワークステーション                                    │
│                                                             │
│  ┌────────────────────────────────────────────────────┐    │
│  │ ローカル Function (localhost:7071)                 │    │
│  │ - InlineRsaPrivateKey（テスト キー）               │    │
│  │ - UseKeyVault = false                              │    │
│  │ - Azurite（ローカル Blob Storage）                 │    │
│  └────────────────┬───────────────────────────────────┘    │
│                   │                                         │
│                   ▼                                         │
│  ┌────────────────────────────────────────────────────┐    │
│  │ ngrok（パブリック HTTPS トンネル）                  │    │
│  │ https://abc123.ngrok-free.app →                    │    │
│  │   http://localhost:7071                            │    │
│  └────────────────┬───────────────────────────────────┘    │
└───────────────────┼─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────────────────┐
│  External ID テスト テナント（クラウド）                     │
│  - カスタム拡張機能: https://abc123.ngrok-free.app          │
│  - OnPasswordSubmit リスナー（優先度 500）                  │
└─────────────────────────────────────────────────────────────┘
```

**特徴**：
- 高速な反復サイクル（デプロイメントなし）
- ブレークポイントを使用した完全なデバッグ
- インライン RSA キー（Key Vault 依存なし）
- パブリック エンドポイント用の ngrok（External ID → ローカル Function）

### 9.2 本番環境（🔜 v2.0 で提供予定）

**ステータス**: 目標アーキテクチャ設計が提供されています。Key Vault、プライベート エンドポイント、VNet 統合は v2.0 で完全に実装されます。

**実装タイムライン**: 完全な自動化とデプロイメント テンプレートを備えた v2.0 リリースで計画されています。

```
┌─────────────────────────────────────────────────────────────┐
│  Azure サブスクリプション（本番環境）                        │
│                                                             │
│  ┌────────────────────────────────────────────────────┐    │
│  │ Azure Function App（Linux Premium EP1）            │    │
│  │ - システム割り当てマネージド ID                    │    │
│  │ - VNet 統合（App サブネット）                      │    │
│  │ - Application Insights モニタリング                │    │
│  │ - カスタム ドメイン + SSL 証明書                   │    │
│  │ - オートスケール: 1-20 インスタンス                │    │
│  └────────────────┬───────────────────────────────────┘    │
│                   │                                         │
│                   │  プライベート ネットワーク              │
│                   ▼                                         │
│  ┌────────────────────────────────────────────────────┐    │
│  │ プライベート エンドポイント サブネット              │    │
│  │ ┌────────────┐  ┌────────────┐  ┌────────────┐    │    │
│  │ │ PE: Key    │  │ PE: Blob   │  │ PE: Queue  │    │    │
│  │ │ Vault      │  │ Storage    │  │            │    │    │
│  │ └────────────┘  └────────────┘  └────────────┘    │    │
│  └────────────────────────────────────────────────────┘    │
│                                                             │
│  ┌────────────────────────────────────────────────────┐    │
│  │ Application Insights (Log Analytics Workspace)     │    │
│  │ - 90 日間の保持                                    │    │
│  │ - カスタム ダッシュボード                          │    │
│  │ - アラート ルール                                  │    │
│  └────────────────────────────────────────────────────┘    │
└───────────────────┼─────────────────────────────────────────┘
                    │
                    │  HTTPS（パブリック インターネット）
                    ▼
┌─────────────────────────────────────────────────────────────┐
│  External ID 本番テナント                                    │
│  - カスタム拡張機能: https://func.contoso.com/api/JitAuth   │
│  - OnPasswordSubmit リスナー（優先度 500）                  │
│  - 拡張機能アプリ登録に構成された公開キー                   │
└─────────────────────────────────────────────────────────────┘
```

**特徴**：
- SFI 準拠のプライベート ネットワーク アーキテクチャ
- すべての Azure リソース アクセスにマネージド ID
- Key Vault の本番 RSA キー（HSM を備えた Premium ティア）
- リクエスト ボリュームに基づくオートスケール
- 包括的なモニタリングとアラート


## 10. 運用上の考慮事項

### 10.1 モニタリングとダッシュボード

#### 主要メトリクス

**移行の進捗**：
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

**JIT 移行成功率**：
```kql
customMetrics
| where name == "JIT.MigrationsCompleted"
| summarize MigrationsCompleted = sum(value) by bin(timestamp, 1h)
| render timechart
```

**API スロットリング イベント**：
```kql
traces
| where message contains "throttle" or message contains "429"
| summarize ThrottleCount = count() by bin(timestamp, 5m), severity = severityLevel
| render timechart
```

### 10.2 アラート戦略

#### 重大アラート

**1. JIT Function 障害（>5% エラー率）**
```kql
customEvents
| where name == "JIT_Migration_Completed"
| extend Result = tostring(customDimensions.Result)
| summarize 
    Total = count(),
    Failures = countif(Result == "Failure")
    by bin(timestamp, 5m)
| extend ErrorRate = (Failures * 100.0) / Total
| where ErrorRate > 5.0
```

**2. インポート/エクスポート停滞（30 分間進捗なし）**
```kql
traces
| where message contains "RUN SUMMARY"
| summarize LastRun = max(timestamp)
| where LastRun < ago(30m)
```

**3. Key Vault アクセス障害**
```kql
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.KEYVAULT"
| where ResultSignature == "Unauthorized"
| summarize FailureCount = count() by bin(TimeGenerated, 5m)
| where FailureCount > 3
```

---
