// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Configuration;

/// <summary>
/// インポート操作の構成オプション。
/// </summary>
public class ImportOptions
{
    /// <summary>
    /// B2C から External ID への属性名マッピングを取得または設定します。
    /// キー = B2C のソース属性名、値 = External ID のターゲット属性名。
    /// 例: { "extension_abc123_CustomerId": "extension_xyz789_LegacyId" }
    /// </summary>
    public Dictionary<string, string> AttributeMappings { get; set; } = new();

    /// <summary>
    /// インポートから除外するフィールドのリストを取得または設定します。
    /// これらのフィールドは B2C から External ID にコピーされません。
    /// 例: ["createdDateTime", "lastPasswordChangeDateTime"]
    /// </summary>
    public List<string> ExcludeFields { get; set; } = new();

    /// <summary>
    /// 移行固有の属性構成を取得または設定します。
    /// </summary>
    public MigrationAttributesOptions MigrationAttributes { get; set; } = new();
}

/// <summary>
/// 移行固有の属性（B2CObjectId、RequireMigration）の構成。
/// </summary>
public class MigrationAttributesOptions
{
    /// <summary>
    /// 元の B2C ObjectId を External ID に保存するかどうかを取得または設定します。
    /// 既定値: true。
    /// </summary>
    public bool StoreB2CObjectId { get; set; } = true;

    /// <summary>
    /// B2C ObjectId を保存するターゲット属性名を取得または設定します。
    /// StoreB2CObjectId が true の場合のみ使用されます。
    /// 既定値: "extension_{ExtensionAppId}_B2CObjectId"
    /// </summary>
    public string? B2CObjectIdTarget { get; set; }

    /// <summary>
    /// External ID で RequireMigration フラグを設定するかどうかを取得または設定します。
    /// 既定値: true。
    /// </summary>
    public bool SetRequireMigration { get; set; } = true;

    /// <summary>
    /// RequireMigration フラグのターゲット属性名を取得または設定します。
    /// SetRequireMigration が true の場合のみ使用されます。
    /// 既定値: "extension_{ExtensionAppId}_RequireMigration"
    /// </summary>
    public string? RequireMigrationTarget { get; set; }

    /// <summary>
    /// ユーザーが既に存在する場合に拡張属性を上書きするかどうかを取得または設定します。
    /// true の場合、ユーザーが存在しても B2CObjectId と移行フラグを更新します。
    /// テストやインポートの再実行に便利です。
    /// 既定値: false
    /// </summary>
    public bool OverwriteExtensionAttributes { get; set; } = false;

    /// <summary>
    /// メール + パスワードの代わりにメール OTP（パスワードレス）を使用するかどうかを取得または設定します。
    /// true の場合、emailAddress ID の代わりにフェデレーション ID（issuer="mail"）を作成します。
    /// これは External ID でメール OTP を介して認証するユーザー向けです。
    /// false の場合、JIT 移行によるパスワードベース認証用の emailAddress ID を作成します。
    /// 既定値: false（JIT 移行でメール + パスワードを使用）
    /// </summary>
    public bool UseEmailOtp { get; set; } = false;
}
