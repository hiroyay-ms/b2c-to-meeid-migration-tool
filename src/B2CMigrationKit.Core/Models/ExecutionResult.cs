// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Models;

/// <summary>
/// オーケストレーション実行の結果を表します。
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// 実行が成功したかどうかを取得または設定します。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 実行が失敗した場合のエラーメッセージを取得または設定します。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 例外が発生した場合の例外を取得または設定します。
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 実行の開始時刻を取得または設定します。
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// 実行の終了時刻を取得または設定します。
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// 実行の所要時間を取得します。
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// メトリクスとカウントを含む実行サマリーを取得または設定します。
    /// </summary>
    public RunSummary? Summary { get; set; }

    /// <summary>
    /// 実行に関する追加メタデータを取得または設定します。
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 成功した実行結果を作成します。
    /// </summary>
    public static ExecutionResult CreateSuccess(RunSummary? summary = null)
    {
        return new ExecutionResult
        {
            Success = true,
            Summary = summary,
            EndTime = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// 失敗した実行結果を作成します。
    /// </summary>
    public static ExecutionResult CreateFailure(string errorMessage, Exception? exception = null)
    {
        return new ExecutionResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception,
            EndTime = DateTimeOffset.UtcNow
        };
    }
}
