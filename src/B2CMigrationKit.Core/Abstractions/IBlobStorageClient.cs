// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Abstractions;

/// <summary>
/// 移行データ用の Azure Blob Storage 操作へのアクセスを提供します。
/// </summary>
public interface IBlobStorageClient
{
    /// <summary>
    /// JSON コンテンツを Blob に書き込みます。
    /// </summary>
    /// <param name="containerName">コンテナ名。</param>
    /// <param name="blobName">Blob 名。</param>
    /// <param name="jsonContent">書き込む JSON コンテンツ。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    Task WriteBlobAsync(
        string containerName,
        string blobName,
        string jsonContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Blob から JSON コンテンツを読み取ります。
    /// </summary>
    /// <param name="containerName">コンテナ名。</param>
    /// <param name="blobName">Blob 名。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>Blob の JSON コンテンツ。</returns>
    Task<string> ReadBlobAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// オプションのプレフィックスを使用してコンテナ内のすべての Blob を一覧表示します。
    /// </summary>
    /// <param name="containerName">コンテナ名。</param>
    /// <param name="prefix">Blob をフィルタリングするためのオプションのプレフィックス。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>Blob 名のコレクション。</returns>
    Task<IEnumerable<string>> ListBlobsAsync(
        string containerName,
        string? prefix = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Blob が存在するかどうかを確認します。
    /// </summary>
    /// <param name="containerName">コンテナ名。</param>
    /// <param name="blobName">Blob 名。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>Blob が存在する場合は true、それ以外は false。</returns>
    Task<bool> BlobExistsAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// コンテナが存在することを確認し、必要に応じて作成します。
    /// </summary>
    /// <param name="containerName">コンテナ名。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    Task EnsureContainerExistsAsync(
        string containerName,
        CancellationToken cancellationToken = default);
}
