// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Abstractions;

/// <summary>
/// 移行ツールキット用のテレメトリおよび可観測性サービスを提供します。
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// カスタムイベントを追跡します。
    /// </summary>
    /// <param name="eventName">イベントの名前。</param>
    /// <param name="properties">イベントに関連付けられたオプションのプロパティ。</param>
    void TrackEvent(string eventName, IDictionary<string, string>? properties = null);

    /// <summary>
    /// メトリック値を追跡します。
    /// </summary>
    /// <param name="metricName">メトリックの名前。</param>
    /// <param name="value">メトリックの値。</param>
    /// <param name="properties">メトリックに関連付けられたオプションのプロパティ。</param>
    void TrackMetric(string metricName, double value, IDictionary<string, string>? properties = null);

    /// <summary>
    /// 例外を追跡します。
    /// </summary>
    /// <param name="exception">追跡する例外。</param>
    /// <param name="properties">例外に関連付けられたオプションのプロパティ。</param>
    void TrackException(Exception exception, IDictionary<string, string>? properties = null);

    /// <summary>
    /// 依存関係の呼び出しを追跡します（例: Microsoft Graph や Azure Storage への呼び出し）。
    /// </summary>
    /// <param name="dependencyType">依存関係の種類（例: "HTTP"、"Azure Blob"）。</param>
    /// <param name="target">依存関係呼び出しのターゲット。</param>
    /// <param name="name">操作の名前。</param>
    /// <param name="data">呼び出しに関するオプションのデータ。</param>
    /// <param name="duration">呼び出しの所要時間。</param>
    /// <param name="success">呼び出しが成功したかどうか。</param>
    void TrackDependency(
        string dependencyType,
        string target,
        string name,
        string? data,
        TimeSpan duration,
        bool success);

    /// <summary>
    /// カウンターメトリックをインクリメントします。
    /// </summary>
    /// <param name="counterName">カウンターの名前。</param>
    /// <param name="increment">インクリメントする量（デフォルトは 1）。</param>
    void IncrementCounter(string counterName, int increment = 1);

    /// <summary>
    /// すべてのテレメトリデータをフラッシュして送信を確実にします。
    /// </summary>
    Task FlushAsync();
}
