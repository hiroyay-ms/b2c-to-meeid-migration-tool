// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Models;

/// <summary>
/// インポートバッチ操作の監査ログ。
/// </summary>
public class ImportAuditLog
{
    /// <summary>
    /// このバッチが処理されたタイムスタンプを取得または設定します。
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// インポートされたソースBlobファイル名を取得または設定します。
    /// </summary>
    public string SourceBlobName { get; set; } = string.Empty;

    /// <summary>
    /// 処理中のバッチ番号を取得または設定します。
    /// </summary>
    public int BatchNumber { get; set; }

    /// <summary>
    /// このバッチ内のユーザーの総数を取得または設定します。
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// 正常にインポートされたユーザーの数を取得または設定します。
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// インポートに失敗した数を取得または設定します。
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// スキップされたユーザーの数（既に存在する重複）を取得または設定します。
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// 正常にインポートされたユーザーのリストを取得または設定します。
    /// </summary>
    public List<ImportedUserRecord> SuccessfulUsers { get; set; } = new();

    /// <summary>
    /// スキップされたユーザー（重複）のリストを取得または設定します。
    /// </summary>
    public List<SkippedUserRecord> SkippedUsers { get; set; } = new();

    /// <summary>
    /// エラー詳細を含む失敗したユーザーインポートのリストを取得または設定します。
    /// </summary>
    public List<FailedUserRecord> FailedUsers { get; set; } = new();

    /// <summary>
    /// このバッチ操作の所要時間（ミリ秒）を取得または設定します。
    /// </summary>
    public double DurationMs { get; set; }
}

/// <summary>
/// 正常にインポートされたユーザーのレコード。
/// </summary>
public class ImportedUserRecord
{
    /// <summary>
    /// 元のB2C ObjectIdを取得または設定します。
    /// </summary>
    public string B2CObjectId { get; set; } = string.Empty;

    /// <summary>
    /// 新しいExternal ID ObjectIdを取得または設定します。
    /// </summary>
    public string ExternalIdObjectId { get; set; } = string.Empty;

    /// <summary>
    /// ユーザープリンシパル名を取得または設定します。
    /// </summary>
    public string UserPrincipalName { get; set; } = string.Empty;

    /// <summary>
    /// ユーザーの表示名を取得または設定します。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// このユーザーがインポートされたタイムスタンプを取得または設定します。
    /// </summary>
    public DateTimeOffset ImportedAt { get; set; }
}

/// <summary>
/// スキップされたユーザー（重複）のレコード。
/// </summary>
public class SkippedUserRecord
{
    /// <summary>
    /// 元のB2C ObjectIdを取得または設定します。
    /// </summary>
    public string B2CObjectId { get; set; } = string.Empty;

    /// <summary>
    /// ユーザープリンシパル名を取得または設定します。
    /// </summary>
    public string UserPrincipalName { get; set; } = string.Empty;

    /// <summary>
    /// ユーザーの表示名を取得または設定します。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// スキップの理由を取得または設定します。
    /// </summary>
    public string Reason { get; set; } = "Duplicate - User already exists";

    /// <summary>
    /// このユーザーがスキップされたタイムスタンプを取得または設定します。
    /// </summary>
    public DateTimeOffset SkippedAt { get; set; }
}

/// <summary>
/// 失敗したユーザーインポートのレコード。
/// </summary>
public class FailedUserRecord
{
    /// <summary>
    /// 元のB2C ObjectIdを取得または設定します。
    /// </summary>
    public string B2CObjectId { get; set; } = string.Empty;

    /// <summary>
    /// ユーザープリンシパル名を取得または設定します。
    /// </summary>
    public string UserPrincipalName { get; set; } = string.Empty;

    /// <summary>
    /// エラーメッセージを取得または設定します。
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// エラーコード（利用可能な場合）を取得または設定します。
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// この失敗が発生したタイムスタンプを取得または設定します。
    /// </summary>
    public DateTimeOffset FailedAt { get; set; }
}
