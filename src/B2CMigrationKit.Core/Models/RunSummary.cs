// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Models;

/// <summary>
/// メトリクスとカウントを含む実行サマリーを表します。
/// </summary>
public class RunSummary
{
    /// <summary>
    /// 操作の名前を取得または設定します。
    /// </summary>
    public string? OperationName { get; set; }

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
    /// 処理されたアイテムの総数を取得または設定します。
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// 成功したアイテムの数を取得または設定します。
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失敗したアイテムの数を取得または設定します。
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// スキップされたアイテムの数を取得または設定します。
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// スロットリングが発生した回数を取得または設定します。
    /// </summary>
    public int ThrottleCount { get; set; }

    /// <summary>
    /// 実行されたリトライの総数を取得または設定します。
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 1秒あたりのアイテム数（スループット）を取得または設定します。
    /// </summary>
    public double ItemsPerSecond
    {
        get
        {
            var seconds = Duration.TotalSeconds;
            return seconds > 0 ? TotalItems / seconds : 0;
        }
    }

    /// <summary>
    /// 実行のカスタムメトリクスを取得または設定します。
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new();

    /// <summary>
    /// 追加のコンテキストとメタデータを取得または設定します。
    /// </summary>
    public Dictionary<string, string> Context { get; set; } = new();

    /// <summary>
    /// ログ用にフォーマットされたサマリー文字列を返します。
    /// </summary>
    public override string ToString()
    {
        return $"RUN SUMMARY: {OperationName} | Duration: {Duration:hh\\:mm\\:ss} | " +
               $"Total: {TotalItems} | Success: {SuccessCount} | Failed: {FailureCount} | " +
               $"Skipped: {SkippedCount} | Throttles: {ThrottleCount} | " +
               $"Throughput: {ItemsPerSecond:F2} items/sec";
    }
}
