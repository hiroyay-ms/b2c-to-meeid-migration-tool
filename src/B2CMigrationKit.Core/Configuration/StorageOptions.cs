// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel.DataAnnotations;

namespace B2CMigrationKit.Core.Configuration;

/// <summary>
/// Azure Storage の構成オプション。
/// </summary>
public class StorageOptions
{
    /// <summary>
    /// ストレージ アカウントの接続文字列またはサービス URI を取得または設定します。
    /// URI のみを指定してマネージド ID を使用します（例: https://account.blob.core.windows.net）。
    /// </summary>
    [Required]
    public string ConnectionStringOrUri { get; set; } = string.Empty;

    /// <summary>
    /// エクスポートされたユーザー データのコンテナー名を取得または設定します。
    /// </summary>
    [Required]
    public string ExportContainerName { get; set; } = "user-exports";

    /// <summary>
    /// インポート エラーとログのコンテナー名を取得または設定します。
    /// </summary>
    public string ErrorContainerName { get; set; } = "migration-errors";

    /// <summary>
    /// インポート監査ログのコンテナー名を取得または設定します。
    /// </summary>
    public string ImportAuditContainerName { get; set; } = "import-audit";

    /// <summary>
    /// プロファイル同期メッセージのキュー名を取得または設定します。
    /// </summary>
    public string ProfileSyncQueueName { get; set; } = "profile-updates";

    /// <summary>
    /// エクスポート ファイルの BLOB 名プレフィックスを取得または設定します。
    /// </summary>
    public string ExportBlobPrefix { get; set; } = "users_";

    /// <summary>
    /// 認証にマネージド ID を使用するかどうかを取得または設定します（既定値: true）。
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;
}
