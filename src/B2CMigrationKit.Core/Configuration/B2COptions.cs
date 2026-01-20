// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel.DataAnnotations;

namespace B2CMigrationKit.Core.Configuration;

/// <summary>
/// Azure AD B2C の構成オプション。
/// </summary>
public class B2COptions
{
    /// <summary>
    /// B2C テナント ID（GUID 形式）を取得または設定します。
    /// Entra ID への直接認証に使用されます。
    /// </summary>
    [Required]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// B2C テナント ドメイン（例: contoso.onmicrosoft.com）を取得または設定します。
    /// </summary>
    [Required]
    public string TenantDomain { get; set; } = string.Empty;

    /// <summary>
    /// B2C アクセス用のアプリ登録を取得または設定します。
    /// JIT 認証の場合、このアプリには以下が必要です:
    /// - 管理者の同意を得た Directory.Read.All 権限
    /// - クライアント シークレットの構成
    /// - パブリック クライアント フローの有効化
    /// </summary>
    [Required]
    public AppRegistration AppRegistration { get; set; } = new();

    /// <summary>
    /// B2C ポリシーベース認証用の ROPC ポリシー名を取得または設定します（レガシー）。
    /// 注: JIT 移行では、B2C ポリシーではなく Entra ID に直接認証します。
    /// このプロパティは下位互換性のために保持されていますが、JIT フローでは使用されません。
    /// </summary>
    [Obsolete("JIT authentication now uses direct Entra ID ROPC instead of B2C policies")]
    public string? RopcPolicyName { get; set; }

    /// <summary>
    /// カスタム Graph API スコープを取得または設定します（既定値: https://graph.microsoft.com/.default）。
    /// </summary>
    public string[] Scopes { get; set; } = new[] { "https://graph.microsoft.com/.default" };
}

/// <summary>
/// Azure AD のアプリ登録を表します。
/// </summary>
public class AppRegistration
{
    /// <summary>
    /// アプリケーション（クライアント）ID を取得または設定します。
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// クライアント シークレット名（Key Vault への参照または直接値）を取得または設定します。
    /// </summary>
    public string? ClientSecretName { get; set; }

    /// <summary>
    /// クライアント シークレット値を取得または設定します（本番環境では非推奨 - Key Vault を使用してください）。
    /// Entra ID ROPC フローによる JIT 認証に必須です。
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// 証明書ベース認証用の証明書サムプリントを取得または設定します。
    /// </summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// このアプリ登録のフレンドリ名を取得または設定します。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// このアプリ登録が有効かどうかを取得または設定します。
    /// </summary>
    public bool Enabled { get; set; } = true;
}
