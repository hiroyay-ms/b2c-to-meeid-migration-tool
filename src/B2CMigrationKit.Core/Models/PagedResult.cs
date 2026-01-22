// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Models;

/// <summary>
/// Microsoft Graph APIからのページング結果を表します。
/// </summary>
/// <typeparam name="T">結果内のアイテムの型。</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// 現在のページ内のアイテムを取得または設定します。
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// 次のページを取得するためのスキップトークンを取得または設定します。
    /// </summary>
    public string? NextPageToken { get; set; }

    /// <summary>
    /// さらにページがあるかどうかを取得します。
    /// </summary>
    public bool HasMorePages => !string.IsNullOrEmpty(NextPageToken);

    /// <summary>
    /// 現在のページ内のアイテム数を取得します。
    /// </summary>
    public int Count => Items.Count;
}
