// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Abstractions;

/// <summary>
/// 移行操作を実行するためのオーケストレータを表します。
/// </summary>
/// <typeparam name="TResult">オーケストレーションによって返される結果の型。</typeparam>
public interface IOrchestrator<TResult>
{
    /// <summary>
    /// オーケストレーションを非同期で実行します。
    /// </summary>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>オーケストレーションの結果。</returns>
    Task<TResult> ExecuteAsync(CancellationToken cancellationToken = default);
}
