// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Configuration;

/// <summary>
/// テレメトリと可観測性の構成オプション。
/// </summary>
public class TelemetryOptions
{
    /// <summary>
    /// Application Insights の接続文字列を取得または設定します。
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Application Insights のインストルメンテーション キーを取得または設定します（レガシー）。
    /// </summary>
    public string? InstrumentationKey { get; set; }

    /// <summary>
    /// テレメトリが有効かどうかを取得または設定します（既定値: true）。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// テレメトリに Application Insights SDK を使用するかどうかを取得または設定します。
    /// false の場合、コンソール ログのみが使用されます（ローカル開発に便利）。
    /// 既定値: false（コンソールのみ）。
    /// </summary>
    public bool UseApplicationInsights { get; set; } = false;

    /// <summary>
    /// コンソール ログを有効にするかどうかを取得または設定します。
    /// Application Insights と同時に使用できます。
    /// 既定値: true（常にコンソールにログ出力）。
    /// </summary>
    public bool UseConsoleLogging { get; set; } = true;

    /// <summary>
    /// サンプリング レートのパーセンテージを取得または設定します（既定値: 100）。
    /// </summary>
    public double SamplingPercentage { get; set; } = 100.0;

    /// <summary>
    /// 依存関係を追跡するかどうかを取得または設定します（既定値: true）。
    /// </summary>
    public bool TrackDependencies { get; set; } = true;

    /// <summary>
    /// 例外を追跡するかどうかを取得または設定します（既定値: true）。
    /// </summary>
    public bool TrackExceptions { get; set; } = true;

    /// <summary>
    /// すべてのテレメトリに含めるカスタム プロパティを取得または設定します。
    /// </summary>
    public Dictionary<string, string> GlobalProperties { get; set; } = new();
}
