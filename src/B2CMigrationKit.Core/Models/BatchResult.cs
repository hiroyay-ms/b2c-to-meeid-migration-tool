// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Models;

/// <summary>
/// 複数ユーザーに対するバッチ操作の結果を表します。
/// </summary>
public class BatchResult
{
    /// <summary>
    /// バッチ内のアイテムの総数を取得または設定します。
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// 成功した操作の数を取得または設定します。
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失敗した操作の数を取得または設定します。
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// スキップされた操作の数（例：重複）を取得または設定します。
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// 失敗したアイテムの詳細を取得または設定します。
    /// </summary>
    public List<BatchItemFailure> Failures { get; set; } = new();

    /// <summary>
    /// スキップされたユーザー識別子（重複）のリストを取得または設定します。
    /// </summary>
    public List<string> SkippedUserIds { get; set; } = new();

    /// <summary>
    /// 拡張属性の更新対象となる重複ユーザーのリストを取得または設定します。
    /// </summary>
    public List<UserProfile> DuplicateUsers { get; set; } = new();

    /// <summary>
    /// バッチがスロットリングされたかどうかを取得または設定します。
    /// </summary>
    public bool WasThrottled { get; set; }

    /// <summary>
    /// スロットリングされた場合のリトライ待機時間を取得または設定します。
    /// </summary>
    public TimeSpan? RetryAfter { get; set; }

    /// <summary>
    /// バッチ全体が成功したかどうかを取得します。
    /// </summary>
    public bool IsFullySuccessful => FailureCount == 0;
}

/// <summary>
/// バッチ操作における単一アイテムの失敗を表します。
/// </summary>
public class BatchItemFailure
{
    /// <summary>
    /// バッチ内のアイテムのインデックスを取得または設定します。
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// アイテムの識別子（例：ユーザーIDまたはUPN）を取得または設定します。
    /// </summary>
    public string? ItemId { get; set; }

    /// <summary>
    /// エラーメッセージを取得または設定します。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 該当する場合のHTTPステータスコードを取得または設定します。
    /// </summary>
    public int? StatusCode { get; set; }
}
