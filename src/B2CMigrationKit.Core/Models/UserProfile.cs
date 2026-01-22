// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Models;

/// <summary>
/// すべての関連するID属性を持つユーザープロファイルを表します。
/// </summary>
public class UserProfile
{
    /// <summary>
    /// Azure ADにおけるユーザーのオブジェクトIDを取得または設定します。
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// ユーザープリンシパル名（UPN）を取得または設定します。
    /// </summary>
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// ユーザーの表示名を取得または設定します。
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// ユーザーの名（ファーストネーム）を取得または設定します。
    /// </summary>
    public string? GivenName { get; set; }

    /// <summary>
    /// ユーザーの姓（ラストネーム）を取得または設定します。
    /// </summary>
    public string? Surname { get; set; }

    /// <summary>
    /// ユーザーのプライマリメールアドレスを取得または設定します。
    /// </summary>
    public string? Mail { get; set; }

    /// <summary>
    /// 代替メールアドレスを取得または設定します。
    /// </summary>
    public List<string> OtherMails { get; set; } = new();

    /// <summary>
    /// ユーザーの携帯電話番号を取得または設定します。
    /// </summary>
    public string? MobilePhone { get; set; }

    /// <summary>
    /// ユーザーの住所を取得または設定します。
    /// </summary>
    public string? StreetAddress { get; set; }

    /// <summary>
    /// ユーザーの市区町村を取得または設定します。
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// ユーザーの都道府県を取得または設定します。
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// ユーザーの郵便番号を取得または設定します。
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// ユーザーの国を取得または設定します。
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// アカウントが有効かどうかを取得または設定します。
    /// </summary>
    public bool AccountEnabled { get; set; } = true;

    /// <summary>
    /// パスワードプロファイル（ユーザー作成時に使用）を取得または設定します。
    /// </summary>
    public PasswordProfile? PasswordProfile { get; set; }

    /// <summary>
    /// 拡張属性（カスタムプロパティ）を取得または設定します。
    /// キー形式: extension_{appId}_{attributeName}
    /// </summary>
    public Dictionary<string, object> ExtensionAttributes { get; set; } = new();

    /// <summary>
    /// ユーザーのID情報（メール、ユーザー名など）を取得または設定します。
    /// </summary>
    public List<ObjectIdentity> Identities { get; set; } = new();

    /// <summary>
    /// 作成日時を取得または設定します。
    /// </summary>
    public DateTimeOffset? CreatedDateTime { get; set; }

    /// <summary>
    /// 明示的にマッピングされていない追加プロパティを取得または設定します。
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// ユーザー作成用のパスワードプロファイルを表します。
/// </summary>
public class PasswordProfile
{
    /// <summary>
    /// パスワードを取得または設定します。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 次回サインイン時にパスワード変更を強制するかどうかを取得または設定します。
    /// </summary>
    public bool ForceChangePasswordNextSignIn { get; set; } = true;
}

/// <summary>
/// ユーザーに関連付けられたID情報（メール、フェデレーションなど）を表します。
/// </summary>
public class ObjectIdentity
{
    /// <summary>
    /// サインインの種類（例: "emailAddress"、"userName"、"federated"）を取得または設定します。
    /// </summary>
    public string? SignInType { get; set; }

    /// <summary>
    /// 発行者（例: テナントドメインまたは外部IdP）を取得または設定します。
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// 発行者が割り当てたID（実際のID値）を取得または設定します。
    /// </summary>
    public string? IssuerAssignedId { get; set; }
}
