// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Core;

namespace B2CMigrationKit.Core.Abstractions;

/// <summary>
/// レート制限を回避するための並列操作用に複数の Azure AD アプリ登録を管理します。
/// </summary>
public interface ICredentialManager
{
    /// <summary>
    /// ラウンドロビン方式で次に使用可能な資格情報を取得します。
    /// </summary>
    /// <returns>認証用のトークン資格情報。</returns>
    TokenCredential GetNextCredential();

    /// <summary>
    /// インデックスで特定の資格情報を取得します。
    /// </summary>
    /// <param name="index">取得する資格情報のインデックス。</param>
    /// <returns>認証用のトークン資格情報。</returns>
    TokenCredential GetCredential(int index);

    /// <summary>
    /// 使用可能な資格情報の総数を取得します。
    /// </summary>
    int CredentialCount { get; }

    /// <summary>
    /// 資格情報がスロットリングに遭遇したことを報告します。
    /// </summary>
    /// <param name="credentialIndex">スロットリングされた資格情報のインデックス。</param>
    /// <param name="retryAfterSeconds">再試行までの待機秒数。</param>
    void ReportThrottling(int credentialIndex, int retryAfterSeconds);
}
