// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel.DataAnnotations;

namespace B2CMigrationKit.Core.Configuration;

/// <summary>
/// B2C 移行ツールキットのメイン構成オプション。
/// </summary>
public class MigrationOptions
{
    /// <summary>
    /// 構成セクション名。
    /// </summary>
    public const string SectionName = "Migration";

    /// <summary>
    /// Azure AD B2C の構成を取得または設定します。
    /// </summary>
    [Required]
    public B2COptions B2C { get; set; } = new();

    /// <summary>
    /// Entra External ID の構成を取得または設定します。
    /// </summary>
    [Required]
    public ExternalIdOptions ExternalId { get; set; } = new();

    /// <summary>
    /// Azure Storage の構成を取得または設定します。
    /// </summary>
    [Required]
    public StorageOptions Storage { get; set; } = new();

    /// <summary>
    /// Azure Key Vault の構成を取得または設定します。
    /// </summary>
    public KeyVaultOptions? KeyVault { get; set; }

    /// <summary>
    /// テレメトリの構成を取得または設定します。
    /// </summary>
    public TelemetryOptions Telemetry { get; set; } = new();

    /// <summary>
    /// リトライ ポリシーの構成を取得または設定します。
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// エクスポートの構成を取得または設定します。
    /// </summary>
    public ExportOptions Export { get; set; } = new();

    /// <summary>
    /// インポートの構成を取得または設定します。
    /// </summary>
    public ImportOptions Import { get; set; } = new();

    /// <summary>
    /// JIT 認証の構成を取得または設定します。
    /// </summary>
    public JitAuthenticationOptions JitAuthentication { get; set; } = new();

    /// <summary>
    /// 操作のバッチ サイズを取得または設定します（既定値: 100）。
    /// </summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Graph API クエリのページ サイズを取得または設定します（既定値: 100）。
    /// </summary>
    [Range(1, 999)]
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// 詳細ログを有効にするかどうかを取得または設定します（既定値: false）。
    /// </summary>
    public bool VerboseLogging { get; set; } = false;

    /// <summary>
    /// バッチ間の遅延（ミリ秒）を取得または設定します（既定値: 0）。
    /// </summary>
    [Range(0, 10000)]
    public int BatchDelayMs { get; set; } = 0;
}
