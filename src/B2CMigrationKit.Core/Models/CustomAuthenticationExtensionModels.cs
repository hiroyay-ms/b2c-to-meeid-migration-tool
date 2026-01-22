// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace B2CMigrationKit.Core.Models;

/// <summary>
/// External ID カスタム認証拡張機能（OnPasswordSubmit イベント）からのリクエストペイロード。
/// </summary>
public class CustomAuthenticationExtensionRequest
{
    [JsonPropertyName("data")]
    public RequestData? Data { get; set; }
}

public class RequestData
{
    [JsonPropertyName("@odata.type")]
    public string? ODataType { get; set; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    [JsonPropertyName("authenticationEventListenerId")]
    public string? AuthenticationEventListenerId { get; set; }

    [JsonPropertyName("customAuthenticationExtensionId")]
    public string? CustomAuthenticationExtensionId { get; set; }

    [JsonPropertyName("authenticationContext")]
    public AuthenticationContext? AuthenticationContext { get; set; }

    [JsonPropertyName("passwordContext")]
    public PasswordContext? PasswordContext { get; set; }

    [JsonPropertyName("encryptedPasswordContext")]
    public string? EncryptedPasswordContext { get; set; }
}

public class AuthenticationContext
{
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("client")]
    public ClientInfo? Client { get; set; }

    [JsonPropertyName("protocol")]
    public string? Protocol { get; set; }

    [JsonPropertyName("clientServicePrincipal")]
    public ServicePrincipalInfo? ClientServicePrincipal { get; set; }

    [JsonPropertyName("resourceServicePrincipal")]
    public ServicePrincipalInfo? ResourceServicePrincipal { get; set; }

    [JsonPropertyName("user")]
    public UserInfo? User { get; set; }
}

public class ClientInfo
{
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    [JsonPropertyName("market")]
    public string? Market { get; set; }
}

public class ServicePrincipalInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("appId")]
    public string? AppId { get; set; }

    [JsonPropertyName("appDisplayName")]
    public string? AppDisplayName { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

public class UserInfo
{
    [JsonPropertyName("createdDateTime")]
    public string? CreatedDateTime { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("givenName")]
    public string? GivenName { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("mail")]
    public string? Mail { get; set; }

    [JsonPropertyName("preferredLanguage")]
    public string? PreferredLanguage { get; set; }

    [JsonPropertyName("surname")]
    public string? Surname { get; set; }

    [JsonPropertyName("userPrincipalName")]
    public string? UserPrincipalName { get; set; }

    [JsonPropertyName("userType")]
    public string? UserType { get; set; }
}

public class PasswordContext
{
    [JsonPropertyName("userPassword")]
    public string? UserPassword { get; set; }

    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; }
}

/// <summary>
/// External ID カスタム認証拡張機能に返すレスポンス。
/// </summary>
public class CustomAuthenticationExtensionResponse
{
    [JsonPropertyName("data")]
    public ResponseData Data { get; set; }

    public CustomAuthenticationExtensionResponse(ResponseActionType actionType, string? title = null, string? message = null)
    {
        Data = new ResponseData(actionType, title, message);
    }
}

public class ResponseData
{
    [JsonPropertyName("@odata.type")]
    public string ODataType { get; set; } = "microsoft.graph.onPasswordSubmitResponseData";

    [JsonPropertyName("actions")]
    public List<ResponseAction> Actions { get; set; }

    [JsonPropertyName("nonce")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nonce { get; set; }

    public ResponseData(ResponseActionType actionType, string? title = null, string? message = null)
    {
        Actions = new List<ResponseAction> { new ResponseAction(actionType, title, message) };
    }
}

public class ResponseAction
{
    [JsonPropertyName("@odata.type")]
    public string ODataType { get; set; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    public ResponseAction(ResponseActionType actionType, string? title = null, string? message = null)
    {
        ODataType = actionType switch
        {
            ResponseActionType.MigratePassword => "microsoft.graph.passwordsubmit.MigratePassword",
            ResponseActionType.Block => "microsoft.graph.passwordsubmit.Block",
            ResponseActionType.UpdatePassword => "microsoft.graph.passwordsubmit.UpdatePassword",
            ResponseActionType.Retry => "microsoft.graph.passwordsubmit.Retry",
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null)
        };

        Title = title;
        Message = message;
    }
}

/// <summary>
/// カスタム認証拡張機能レスポンスで使用可能なアクションタイプ。
/// </summary>
public enum ResponseActionType
{
    /// <summary>
    /// ソースシステムからユーザーのパスワードを移行します。
    /// External ID はユーザーが提供した値にパスワードを設定します。
    /// </summary>
    MigratePassword,

    /// <summary>
    /// ユーザーのサインインをブロックします。
    /// ユーザーに表示するタイトルとメッセージが必要です。
    /// </summary>
    Block,

    /// <summary>
    /// ユーザーのパスワードを更新します。
    /// </summary>
    UpdatePassword,

    /// <summary>
    /// ユーザーに認証の再試行を求めます。
    /// </summary>
    Retry
}
