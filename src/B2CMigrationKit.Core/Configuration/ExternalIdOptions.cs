// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel.DataAnnotations;

namespace B2CMigrationKit.Core.Configuration;

/// <summary>
/// Microsoft Entra External ID の構成オプション。
/// </summary>
public class ExternalIdOptions
{
    /// <summary>
    /// External ID テナント ID を取得または設定します。
    /// </summary>
    [Required]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// External ID テナント ドメイン（例: contoso.onmicrosoft.com）を取得または設定します。
    /// </summary>
    [Required]
    public string TenantDomain { get; set; } = string.Empty;

    /// <summary>
    /// External ID アクセス用のアプリ登録を取得または設定します。
    /// </summary>
    [Required]
    public AppRegistration AppRegistration { get; set; } = new();

    /// <summary>
    /// カスタム属性用の拡張アプリケーション ID を取得または設定します。
    /// これは拡張属性に使用されるアプリ ID（ハイフンなし）です。
    /// </summary>
    [Required]
    public string ExtensionAppId { get; set; } = string.Empty;

    /// <summary>
    /// 既定のパスワード複雑性ポリシーを取得または設定します。
    /// </summary>
    public PasswordPolicy PasswordPolicy { get; set; } = new();

    /// <summary>
    /// カスタム Graph API スコープを取得または設定します（既定値: https://graph.microsoft.com/.default）。
    /// </summary>
    public string[] Scopes { get; set; } = new[] { "https://graph.microsoft.com/.default" };
}

/// <summary>
/// パスワード複雑性ポリシーの構成。
/// </summary>
public class PasswordPolicy
{
    /// <summary>
    /// パスワードの最小長を取得または設定します（既定値: 8）。
    /// </summary>
    [Range(4, 256)]
    public int MinLength { get; set; } = 8;

    /// <summary>
    /// 大文字が必須かどうかを取得または設定します（既定値: true）。
    /// </summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>
    /// 小文字が必須かどうかを取得または設定します（既定値: true）。
    /// </summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>
    /// 数字が必須かどうかを取得または設定します（既定値: true）。
    /// </summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>
    /// 特殊文字が必須かどうかを取得または設定します（既定値: true）。
    /// </summary>
    public bool RequireSpecialCharacter { get; set; } = true;

    /// <summary>
    /// 許可される特殊文字を取得または設定します。
    /// </summary>
    public string AllowedSpecialCharacters { get; set; } = "!@#$%^&*()-_=+[]{}|;:,.<>?";
}
