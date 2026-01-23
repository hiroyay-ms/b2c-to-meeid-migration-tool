// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using B2CMigrationKit.Core.Models;

namespace B2CMigrationKit.Core.Abstractions;

/// <summary>
/// 組み込みのリトライおよびスロットリング処理を備えた Microsoft Graph API 操作へのアクセスを提供します。
/// </summary>
public interface IGraphClient
{
    /// <summary>
    /// ページング対応でディレクトリからユーザーを取得します。
    /// </summary>
    /// <param name="pageSize">ページごとに取得するユーザー数。</param>
    /// <param name="select">選択するプロパティのカンマ区切りリスト（オプション）。</param>
    /// <param name="filter">OData フィルター式（オプション）。</param>
    /// <param name="skipToken">ページネーション用のスキップトークン（オプション）。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>継続トークン付きのユーザープロファイルのページ。</returns>
    Task<PagedResult<UserProfile>> GetUsersAsync(
        int pageSize = 100,
        string? select = null,
        string? filter = null,
        string? skipToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ディレクトリに新しいユーザーを作成します。
    /// </summary>
    /// <param name="user">作成するユーザープロファイル。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>割り当てられた ID を持つ作成されたユーザープロファイル。</returns>
    Task<UserProfile> CreateUserAsync(
        UserProfile user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// バッチ操作で複数のユーザーを作成します。
    /// </summary>
    /// <param name="users">作成するユーザーのコレクション。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>バッチ操作の結果。</returns>
    Task<BatchResult> CreateUsersBatchAsync(
        IEnumerable<UserProfile> users,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ディレクトリ内の既存ユーザーを更新します。
    /// </summary>
    /// <param name="userId">更新するユーザーの ID。</param>
    /// <param name="updates">更新するプロパティ名と値の辞書。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    Task UpdateUserAsync(
        string userId,
        Dictionary<string, object> updates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// オブジェクト ID でユーザーを取得します。
    /// </summary>
    /// <param name="userId">ユーザーのオブジェクト ID。</param>
    /// <param name="select">選択するプロパティのカンマ区切りリスト（オプション）。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>ユーザープロファイル、見つからない場合は null。</returns>
    Task<UserProfile?> GetUserByIdAsync(
        string userId,
        string? select = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 拡張属性値でユーザーを検索します。
    /// </summary>
    /// <param name="extensionAttributeName">拡張属性の名前。</param>
    /// <param name="value">検索する値。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>一致するユーザープロファイル、見つからない場合は null。</returns>
    Task<UserProfile?> FindUserByExtensionAttributeAsync(
        string extensionAttributeName,
        string value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ユーザーのパスワードを設定します。
    /// </summary>
    /// <param name="userId">ユーザーのオブジェクト ID。</param>
    /// <param name="password">新しいパスワード。</param>
    /// <param name="forceChangePasswordNextSignIn">次回サインイン時にパスワード変更を強制するかどうか。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    Task SetUserPasswordAsync(
        string userId,
        string password,
        bool forceChangePasswordNextSignIn = false,
        CancellationToken cancellationToken = default);
}
