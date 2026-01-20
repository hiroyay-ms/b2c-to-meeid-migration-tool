// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Configuration;

/// <summary>
/// エクスポート操作の構成オプション。
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// エクスポート時に B2C から選択するフィールドを取得または設定します。
    /// カンマ区切りのフィールド名リスト（例: "id,userPrincipalName,displayName"）。
    /// 既定値には標準フィールド + identities が含まれます。
    /// </summary>
    public string SelectFields { get; set; } = "id,userPrincipalName,displayName,givenName,surname,mail,mobilePhone,identities";

    /// <summary>
    /// エクスポートするユーザーの最大数を取得または設定します。
    /// null または 0 の場合、すべてのユーザーをエクスポートします。制限されたデータセットでのテストに便利です。
    /// </summary>
    public int? MaxUsers { get; set; }

    /// <summary>
    /// ユーザーの displayName または userPrincipalName のフィルター パターンを取得または設定します。
    /// 指定した場合、displayName または userPrincipalName にこの値を含むユーザーのみがエクスポートされます。
    /// 大文字小文字を区別しません。例: "MigTest", "contoso", "john.doe"
    /// null または空の場合、すべてのユーザーをエクスポートします（MaxUsers 制限に従います）。
    /// </summary>
    public string? FilterPattern { get; set; }
}
