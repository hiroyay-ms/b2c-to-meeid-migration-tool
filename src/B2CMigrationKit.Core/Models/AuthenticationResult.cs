// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Models;

/// <summary>
/// 資格情報の検証結果を表します。
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// 認証が成功したかどうかを取得または設定します。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 認証が失敗した場合のエラーコードを取得または設定します。
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 認証が失敗した場合のエラー説明を取得または設定します。
    /// </summary>
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// 認証が成功した場合のユーザーのオブジェクトIDを取得または設定します。
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// アカウントがロックアウトされているかどうかを取得または設定します。
    /// </summary>
    public bool IsLockedOut { get; set; }

    /// <summary>
    /// MFAが必要かどうかを取得または設定します。
    /// </summary>
    public bool RequiresMfa { get; set; }

    /// <summary>
    /// 認証試行からの追加コンテキストを取得または設定します。
    /// </summary>
    public Dictionary<string, string> Context { get; set; } = new();

    /// <summary>
    /// 成功した認証結果を作成します。
    /// </summary>
    public static AuthenticationResult CreateSuccess(string userId)
    {
        return new AuthenticationResult
        {
            Success = true,
            UserId = userId
        };
    }

    /// <summary>
    /// 失敗した認証結果を作成します。
    /// </summary>
    public static AuthenticationResult CreateFailure(string errorCode, string errorDescription)
    {
        return new AuthenticationResult
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorDescription = errorDescription
        };
    }
}
