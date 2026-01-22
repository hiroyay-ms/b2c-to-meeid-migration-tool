// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Models;

/// <summary>
/// ユーザーの移行ステータスを表します。
/// </summary>
public enum MigrationStatus
{
    /// <summary>
    /// ユーザーはまだ移行されていません。
    /// </summary>
    NotMigrated = 0,

    /// <summary>
    /// ユーザープロファイルはインポートされましたが、パスワードはまだ移行されていません。
    /// </summary>
    ProfileImported = 1,

    /// <summary>
    /// ユーザーはJIT経由でパスワードを含め完全に移行されました。
    /// </summary>
    FullyMigrated = 2,

    /// <summary>
    /// このユーザーの移行に失敗しました。
    /// </summary>
    Failed = 3,

    /// <summary>
    /// ユーザーは移行中です（進行中）。
    /// </summary>
    InProgress = 4
}

/// <summary>
/// 移行追跡に使用される拡張属性名。
/// </summary>
public static class MigrationExtensionAttributes
{
    /// <summary>
    /// 元のB2CオブジェクトIDを格納するための拡張属性名。
    /// 形式: extension_{appId}_B2CObjectId
    /// </summary>
    public const string B2CObjectId = "B2CObjectId";

    /// <summary>
    /// ユーザーがJIT移行を必要とするかどうかを示す拡張属性名。
    /// 形式: extension_{appId}_RequireMigration
    /// 注: セマンティックな意味はJitAuthenticationOptions.MigrationAttributeNameで設定可能です。
    /// デフォルトの動作: true = 移行が必要、false = 移行済み
    /// </summary>
    public const string RequireMigration = "RequireMigration";

    /// <summary>
    /// 移行タイムスタンプを格納するための拡張属性名。
    /// 形式: extension_{appId}_MigrationDate
    /// </summary>
    public const string MigrationDate = "MigrationDate";

    /// <summary>
    /// アプリIDを含む完全な拡張属性名を取得します。
    /// </summary>
    /// <param name="appId">アプリケーションID（ハイフンなし）。</param>
    /// <param name="attributeName">属性名。</param>
    /// <returns>完全な拡張属性名。</returns>
    public static string GetFullAttributeName(string appId, string attributeName)
    {
        var cleanAppId = appId.Replace("-", string.Empty);
        return $"extension_{cleanAppId}_{attributeName}";
    }
}
