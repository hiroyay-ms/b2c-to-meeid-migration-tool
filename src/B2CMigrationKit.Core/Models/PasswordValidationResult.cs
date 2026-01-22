// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Models;

/// <summary>
/// パスワード複雑性検証の結果を表します。
/// </summary>
public class PasswordValidationResult
{
    /// <summary>
    /// パスワードが有効かどうかを取得または設定します。
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 検証エラーメッセージを取得または設定します。
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// パスワードが最小長要件を満たしているかどうかを取得または設定します。
    /// </summary>
    public bool MeetsLengthRequirement { get; set; }

    /// <summary>
    /// パスワードに大文字が含まれているかどうかを取得または設定します。
    /// </summary>
    public bool HasUppercase { get; set; }

    /// <summary>
    /// パスワードに小文字が含まれているかどうかを取得または設定します。
    /// </summary>
    public bool HasLowercase { get; set; }

    /// <summary>
    /// パスワードに数字が含まれているかどうかを取得または設定します。
    /// </summary>
    public bool HasDigit { get; set; }

    /// <summary>
    /// パスワードに特殊文字が含まれているかどうかを取得または設定します。
    /// </summary>
    public bool HasSpecialCharacter { get; set; }

    /// <summary>
    /// 成功した検証結果を作成します。
    /// </summary>
    public static PasswordValidationResult CreateValid()
    {
        return new PasswordValidationResult
        {
            IsValid = true,
            MeetsLengthRequirement = true,
            HasUppercase = true,
            HasLowercase = true,
            HasDigit = true,
            HasSpecialCharacter = true
        };
    }

    /// <summary>
    /// 失敗した検証結果を作成します。
    /// </summary>
    public static PasswordValidationResult CreateInvalid(params string[] errors)
    {
        return new PasswordValidationResult
        {
            IsValid = false,
            Errors = errors.ToList()
        };
    }
}
