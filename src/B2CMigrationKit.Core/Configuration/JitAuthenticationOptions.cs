// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Configuration;

/// <summary>
/// 移行中の JIT（Just-In-Time）認証の構成オプション。
/// </summary>
public class JitAuthenticationOptions
{
    /// <summary>
    /// Azure Key Vault 内の RSA 秘密キー名を取得または設定します。
    /// このキーは External ID から送信されるパスワード コンテキストの復号化に使用されます。
    /// 既定値: "JIT-RSA-PrivateKey"
    /// </summary>
    public string RsaKeyName { get; set; } = "JIT-RSA-PrivateKey";

    /// <summary>
    /// RSA 秘密キーの保存に Azure Key Vault を使用するかどうかを取得または設定します。
    /// true の場合、マネージド ID を使用して Key Vault からキーを取得します。
    /// false の場合、構成内のインライン キーにフォールバックします（ローカル開発専用）。
    /// 既定値: true（本番環境では常に Key Vault を使用する必要があります）
    /// </summary>
    public bool UseKeyVault { get; set; } = true;

    /// <summary>
    /// ローカル開発用の PEM 形式のインライン RSA 秘密キーを取得または設定します。
    /// 警告: ローカル開発にのみ使用してください。実際のキーをソース管理にコミットしないでください。
    /// 本番デプロイでは Azure Key Vault を使用する必要があります（UseKeyVault = true）。
    /// </summary>
    public string? InlineRsaPrivateKey { get; set; }

    /// <summary>
    /// JIT 認証操作のタイムアウト（秒）を取得または設定します。
    /// External ID には 2 秒のタイムアウトがあるため、これはそれ未満である必要があります。
    /// 既定値: 1.5 秒
    /// </summary>
    public double TimeoutSeconds { get; set; } = 1.5;

    /// <summary>
    /// 最初の取得後に RSA 秘密キーをメモリにキャッシュするかどうかを取得または設定します。
    /// Key Vault 呼び出しを削減しますが、キーをメモリに保持します。
    /// 既定値: true
    /// </summary>
    public bool CachePrivateKey { get; set; } = true;

    /// <summary>
    /// JIT 移行のテスト モードを有効にするかどうかを取得または設定します。
    /// true の場合、B2C ROPC 検証とパスワード複雑性チェックをスキップします。
    /// 警告: ローカル開発とテストにのみ使用してください。本番環境では false である必要があります。
    /// 既定値: false（本番モード）
    /// </summary>
    public bool TestMode { get; set; } = false;

    /// <summary>
    /// JIT 移行をトリガーするために使用されるカスタム属性名を取得または設定します。
    /// この属性は External ID カスタム認証拡張機能で構成する必要があります。
    /// この属性が true かつパスワードが一致しない場合、JIT がトリガーされます。
    /// 既定値: "RequiresMigration"
    /// </summary>
    public string MigrationAttributeName { get; set; } = "RequiresMigration";
}
