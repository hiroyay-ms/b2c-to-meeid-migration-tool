日本語 | **[English](CONTRIBUTING.md)**

# B2C Migration Kit への貢献

B2C Migration Kit への貢献にご興味をお持ちいただきありがとうございます！このプロジェクトは、Azure AD B2C から Microsoft Entra External ID へのユーザー移行のためのサンプル実装です。

## 🎯 プロジェクトのステータス

このプロジェクトは現在、Just-In-Time パスワード移行を紹介する**プレビュー/サンプル実装**です。貢献を歓迎しますが、まだ本番環境向けではないことにご注意ください。

## 📋 貢献の方法

- **バグ報告**: 再現手順を含む詳細な問題レポートを送信
- **機能提案**: 機能強化や新機能の提案
- **ドキュメント**: ガイド、サンプル、トラブルシューティングの改善
- **コード**: バグ修正や機能を含むプルリクエストの送信

## 🚀 はじめに

### 前提条件

- .NET 8.0 SDK 以降
- PowerShell 7.0 以上（自動化スクリプト用）
- B2C および External ID テナントを持つ Azure サブスクリプション
- Visual Studio 2022 または VS Code

### ローカル開発環境のセットアップ

1. **リポジトリをクローン**
   ```bash
   git clone https://github.com/alvesfabi/B2C-Migration-Kit.git
   cd B2C-Migration-Kit
   ```

2. **依存関係をインストール**
   ```bash
   dotnet restore
   ```

3. **ローカル設定を構成**
   ```bash
   cd src/B2CMigrationKit.Console
   cp appsettings.json appsettings.local.json
   # appsettings.local.json をテナントの資格情報で編集
   ```

4. **Azurite（ローカル ストレージ エミュレーター）をセットアップ**
   ```bash
   npm install -g azurite
   azurite --silent --location .azurite --debug .azurite\debug.log
   ```

5. **ソリューションをビルド**
   ```bash
   dotnet build
   ```

詳細なセットアップ手順については、[開発者ガイド](docs/DEVELOPER_GUIDE_jp.md)を参照してください。

## 🔨 変更を加える

### ブランチ戦略

- `main` - リリース用の安定ブランチ
- `feature/*` - 新機能や機能強化
- `bugfix/*` - バグ修正
- `docs/*` - ドキュメントの更新

### コーディング規約

- **C#**: [Microsoft C# コーディング規約](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)に従う
- **命名規則**: クラス/メソッドは PascalCase、変数は camelCase を使用
- **コメント**: パブリック API には XML ドキュメントを追加
- **エラー処理**: 適切なログ記録を伴う構造化された例外を使用
- **Async/Await**: I/O 操作には非同期パターンを使用

### テスト

- xUnit を使用して新機能のユニットテストを作成
- 既存のテストがパスすることを確認: `dotnet test`
- PR を送信する前に Azurite でローカルテストを実行

### コミットメッセージ

明確で説明的なコミットメッセージを使用してください：

```
カスタム属性マッピングのサポートを追加

- 柔軟な属性変換を実装
- 構成の検証を追加
- サンプルを含むドキュメントを更新
```

## 📝 プルリクエストのプロセス

1. 変更を説明する**Issue を作成**（些細でない変更の場合）
2. リポジトリを**フォーク**し、機能ブランチを作成
3. コーディング規約に従って**変更を加える**
4. 新機能の**テストを追加**
5. 必要に応じて**ドキュメントを更新**
6. 以下を含む**プルリクエストを送信**：
   - 変更の明確な説明
   - 関連する Issue へのリンク
   - スクリーンショット（UI の変更の場合）
   - テスト結果

### PR チェックリスト

- [ ] コードがプロジェクトのコーディング規約に従っている
- [ ] テストが追加/更新され、パスしている
- [ ] ドキュメントが更新されている（README、ガイド、コメント）
- [ ] コードにシークレットや資格情報が含まれていない
- [ ] コミットメッセージが明確で説明的である
- [ ] PR の説明が何を/なぜを説明している

## 🔐 セキュリティ

- **シークレットをコミットしない**: 構成ファイルを使用（gitignore 済み）
- **脆弱性の報告**: 責任ある開示については [SECURITY.md](SECURITY.md) を参照
- **依存関係の確認**: サードパーティ パッケージが安全であることを確認

## 📜 行動規範

このプロジェクトは [Microsoft オープンソース行動規範](https://opensource.microsoft.com/codeofconduct/)を採用しています。

詳細については[行動規範 FAQ](https://opensource.microsoft.com/codeofconduct/faq/)を参照するか、追加の質問やコメントについては [opencode@microsoft.com](mailto:opencode@microsoft.com) までお問い合わせください。

## 📄 貢献者ライセンス契約

このプロジェクトは貢献と提案を歓迎します。ほとんどの貢献には、貢献を使用する権利を付与する権限があることを宣言する貢献者ライセンス契約（CLA）に同意する必要があります。

プルリクエストを送信すると、CLA ボットが自動的に CLA を提供する必要があるかどうかを判断し、PR を適切に装飾します（例：ラベル、コメント）。ボットが提供する指示に従ってください。CLA を使用するすべてのリポジトリで一度だけ行う必要があります。

## 🎓 リソース

- [開発者ガイド](docs/DEVELOPER_GUIDE_jp.md) - 技術的な実装の詳細
- [アーキテクチャ ガイド](docs/ARCHITECTURE_GUIDE_jp.md) - システム設計とアーキテクチャ
- [スクリプト README](scripts/README.md) - 自動化スクリプトのドキュメント
- [Microsoft Entra ドキュメント](https://learn.microsoft.com/entra/)

## 💬 質問がありますか？

- **GitHub Issues**: バグと機能リクエスト用
- **GitHub Discussions**: 質問と一般的なディスカッション用

貢献いただきありがとうございます！🎉
