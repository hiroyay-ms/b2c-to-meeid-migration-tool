// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel.DataAnnotations;

namespace B2CMigrationKit.Core.Configuration;

/// <summary>
/// リトライ ポリシーとスロットリング処理の構成オプション。
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// 最大リトライ回数を取得または設定します（既定値: 5）。
    /// </summary>
    [Range(0, 20)]
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// 初回リトライ遅延（ミリ秒）を取得または設定します（既定値: 1000）。
    /// </summary>
    [Range(100, 60000)]
    public int InitialDelayMs { get; set; } = 1000;

    /// <summary>
    /// 最大リトライ遅延（ミリ秒）を取得または設定します（既定値: 30000）。
    /// </summary>
    [Range(1000, 300000)]
    public int MaxDelayMs { get; set; } = 30000;

    /// <summary>
    /// 指数バックオフ乗数を取得または設定します（既定値: 2.0）。
    /// </summary>
    [Range(1.0, 10.0)]
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// API レスポンスの Retry-After ヘッダーを尊重するかどうかを取得または設定します（既定値: true）。
    /// </summary>
    public bool UseRetryAfterHeader { get; set; } = true;

    /// <summary>
    /// 個々の操作のタイムアウト（秒）を取得または設定します（既定値: 120）。
    /// </summary>
    [Range(10, 600)]
    public int OperationTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// リトライをトリガーする HTTP ステータス コードを取得または設定します。
    /// </summary>
    public int[] RetryableStatusCodes { get; set; } = new[] { 429, 500, 502, 503, 504 };
}
