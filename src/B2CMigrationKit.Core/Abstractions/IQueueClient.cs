// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using B2CMigrationKit.Core.Models;

namespace B2CMigrationKit.Core.Abstractions;

/// <summary>
/// 非同期プロファイル同期のための Azure Queue 操作へのアクセスを提供します。
/// </summary>
public interface IQueueClient
{
    /// <summary>
    /// プロファイル更新メッセージをキューに送信します。
    /// </summary>
    /// <param name="queueName">キュー名。</param>
    /// <param name="message">プロファイル更新メッセージ。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    Task SendMessageAsync(
        string queueName,
        ProfileUpdateMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// キューからメッセージを受信します。
    /// </summary>
    /// <param name="queueName">キュー名。</param>
    /// <param name="maxMessages">受信するメッセージの最大数。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>プロファイル更新メッセージのコレクション。</returns>
    Task<IEnumerable<ProfileUpdateMessage>> ReceiveMessagesAsync(
        string queueName,
        int maxMessages = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 処理が成功した後、キューからメッセージを削除します。
    /// </summary>
    /// <param name="queueName">キュー名。</param>
    /// <param name="messageId">メッセージ ID。</param>
    /// <param name="popReceipt">受信したメッセージのポップレシート。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    Task DeleteMessageAsync(
        string queueName,
        string messageId,
        string popReceipt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// キューが存在することを確認し、必要に応じて作成します。
    /// </summary>
    /// <param name="queueName">キュー名。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    Task EnsureQueueExistsAsync(
        string queueName,
        CancellationToken cancellationToken = default);
}
