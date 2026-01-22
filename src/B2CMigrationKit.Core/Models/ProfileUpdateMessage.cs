// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Models;

/// <summary>
/// キューベースの非同期同期用のプロファイル更新メッセージを表します。
/// </summary>
public class ProfileUpdateMessage
{
    /// <summary>
    /// 一意のメッセージIDを取得または設定します。
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// ポップレシート（メッセージ削除に使用）を取得または設定します。
    /// </summary>
    public string? PopReceipt { get; set; }

    /// <summary>
    /// 変更が発生したソースシステムを取得または設定します。
    /// </summary>
    public UpdateSource Source { get; set; }

    /// <summary>
    /// ソースシステムにおけるユーザーのIDを取得または設定します。
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// ターゲットシステムにおけるユーザーのID（既知の場合）を取得または設定します。
    /// </summary>
    public string? TargetUserId { get; set; }

    /// <summary>
    /// 関連付けのためのB2CオブジェクトIDを取得または設定します。
    /// </summary>
    public string? B2CObjectId { get; set; }

    /// <summary>
    /// ユーザープリンシパル名を取得または設定します。
    /// </summary>
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// 更新されたプロパティを取得または設定します。
    /// </summary>
    public Dictionary<string, object> UpdatedProperties { get; set; } = new();

    /// <summary>
    /// 更新が発生したタイムスタンプを取得または設定します。
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 追跡用の相関IDを取得または設定します。
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// プロファイル更新のソースシステムを指定します。
/// </summary>
public enum UpdateSource
{
    /// <summary>
    /// 更新はAzure AD B2Cから発生しました。
    /// </summary>
    B2C,

    /// <summary>
    /// 更新はEntra External IDから発生しました。
    /// </summary>
    ExternalId
}
