// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Abstractions;

/// <summary>
/// Azure Key Vault に保存されたシークレットへのアクセスを提供します。
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// 名前でシークレット値を取得します。
    /// </summary>
    /// <param name="secretName">シークレットの名前。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>シークレットの値。</returns>
    Task<string> GetSecretAsync(
        string secretName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// シークレット値を設定します。
    /// </summary>
    /// <param name="secretName">シークレットの名前。</param>
    /// <param name="secretValue">保存する値。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    Task SetSecretAsync(
        string secretName,
        string secretValue,
        CancellationToken cancellationToken = default);
}
