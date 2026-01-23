// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using B2CMigrationKit.Core.Models;

namespace B2CMigrationKit.Core.Abstractions;

/// <summary>
/// JIT 移行時の資格情報検証のための認証サービスを提供します。
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// ROPC フローを使用して Azure AD B2C に対してユーザー資格情報を検証します。
    /// </summary>
    /// <param name="username">ユーザー名（メールアドレスまたは UPN）。</param>
    /// <param name="password">ユーザーのパスワード。</param>
    /// <param name="cancellationToken">操作をキャンセルするためのトークン。</param>
    /// <returns>成功または失敗を示す認証結果。</returns>
    Task<AuthenticationResult> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// パスワードが複雑性要件を満たしているかどうかを検証します。
    /// </summary>
    /// <param name="password">検証するパスワード。</param>
    /// <returns>エラーメッセージを含む検証結果。</returns>
    PasswordValidationResult ValidatePasswordComplexity(string password);
}
