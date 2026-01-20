// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using B2CMigrationKit.Core.Abstractions;
using B2CMigrationKit.Core.Configuration;
using B2CMigrationKit.Core.Models;
using B2CMigrationKit.Core.Services.Orchestrators;
using Jose;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace B2CMigrationKit.Function;

/// <summary>
/// ログイン時の Just-In-Time ユーザー移行用 Azure Function。
/// External ID カスタム認証拡張機能（OnPasswordSubmit イベント）によって呼び出されます。
/// </summary>
public class JitAuthenticationFunction
{
    private readonly JitMigrationService _jitService;
    private readonly ISecretProvider? _secretProvider;
    private readonly JitAuthenticationOptions _jitOptions;
    private readonly MigrationOptions _migrationOptions;
    private readonly ILogger<JitAuthenticationFunction> _logger;

    // キャッシュされた RSA 秘密キー（Key Vault またはインライン構成から読み込み）
    private string? _cachedPrivateKey;
    private readonly SemaphoreSlim _keyLoadLock = new(1, 1);

    public JitAuthenticationFunction(
        JitMigrationService jitService,
        IOptions<MigrationOptions> options,
        ILogger<JitAuthenticationFunction> logger,
        ISecretProvider? secretProvider = null)
    {
        _jitService = jitService ?? throw new ArgumentNullException(nameof(jitService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _secretProvider = secretProvider;
        _migrationOptions = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _jitOptions = options?.Value?.JitAuthentication ?? throw new ArgumentNullException(nameof(options));

        // 構成の検証
        if (_jitOptions.UseKeyVault && _secretProvider == null)
        {
            throw new InvalidOperationException(
                "JIT Authentication is configured to use Key Vault (UseKeyVault=true) but ISecretProvider is not registered. " +
                "Ensure Key Vault is configured in MigrationOptions.KeyVault.");
        }

        if (!_jitOptions.UseKeyVault && string.IsNullOrEmpty(_jitOptions.InlineRsaPrivateKey))
        {
            throw new InvalidOperationException(
                "JIT Authentication is configured to use inline key (UseKeyVault=false) but InlineRsaPrivateKey is not set. " +
                "For local development, provide the RSA private key in configuration.");
        }
    }

    [Function("JitAuthentication")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        FunctionContext context)
    {
        var requestId = Guid.NewGuid().ToString();
        var startTime = DateTimeOffset.UtcNow;
        var remoteIp = req.Headers.Contains("X-Forwarded-For")
            ? req.Headers.GetValues("X-Forwarded-For").FirstOrDefault()
            : "unknown";

        _logger.LogInformation(
            "[JIT Function] HTTP {Method} received | RequestId: {RequestId} | RemoteIP: {RemoteIP}",
            req.Method, requestId, remoteIp);

        // GET リクエストの処理 - External ID がエンドポイントを検証するために使用
        if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[JIT Function] GET request - Endpoint validation | RequestId: {RequestId}", requestId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("JIT Authentication Endpoint - Ready");
            return response;
        }

        try
        {
            // External ID カスタム認証拡張機能リクエストの解析
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation(
                "[JIT Function] Parsing External ID payload | RequestId: {RequestId} | BodyLength: {Length}",
                requestId, requestBody.Length);

            var extRequest = JsonSerializer.Deserialize<CustomAuthenticationExtensionRequest>(
                requestBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (extRequest?.Data == null)
            {
                _logger.LogWarning(
                    "[JIT Function] ⚠️ Invalid payload - Missing data object | RequestId: {RequestId}",
                    requestId);

                return await CreateExtensionResponseAsync(req, new JitMigrationResult
                {
                    ActionType = ResponseActionType.Block,
                    Title = "Invalid Request",
                    Message = "The authentication request is malformed."
                });
            }

            // External ID ペイロードからユーザー情報を抽出
            var userId = extRequest.Data.AuthenticationContext?.User?.Id;
            var userPrincipalName = extRequest.Data.AuthenticationContext?.User?.UserPrincipalName;
            var correlationId = extRequest.Data.AuthenticationContext?.CorrelationId ?? requestId;

            // パスワードの抽出 - 暗号化されたコンテキストとプレーンテキストの両方を処理
            string? userPassword = null;
            string? nonce = null;

            if (!string.IsNullOrEmpty(extRequest.Data.EncryptedPasswordContext))
            {
                _logger.LogInformation(
                    "[JIT Function] Decrypting password context | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                    correlationId, requestId);

                (userPassword, nonce) = await DecryptPasswordContext(extRequest.Data.EncryptedPasswordContext, correlationId, requestId);

                if (string.IsNullOrEmpty(userPassword))
                {
                    _logger.LogError(
                        "[JIT Function] Failed to decrypt password context | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                        correlationId, requestId);

                    return await CreateExtensionResponseAsync(req, new JitMigrationResult
                    {
                        ActionType = ResponseActionType.Block,
                        Title = "Decryption Error",
                        Message = "Unable to process authentication request."
                    }, nonce);
                }
            }
            else
            {
                // プレーンテキストのパスワード コンテキストにフォールバック（テスト用）
                userPassword = extRequest.Data.PasswordContext?.UserPassword;
                nonce = extRequest.Data.PasswordContext?.Nonce;
            }

            _logger.LogInformation(
                "[JIT Function] Parsed External ID payload | UserId: {UserId} | UPN: {UPN} | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                userId, userPrincipalName, correlationId, requestId);

            // 必須フィールドの検証
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userPassword))
            {
                _logger.LogWarning(
                    "[JIT Function] ⚠️ Missing required fields | UserId: {UserId} | HasPassword: {HasPassword} | RequestId: {RequestId}",
                    userId ?? "null", !string.IsNullOrEmpty(userPassword), requestId);

                return await CreateExtensionResponseAsync(req, new JitMigrationResult
                {
                    ActionType = ResponseActionType.Block,
                    Title = "Invalid Request",
                    Message = "Required authentication information is missing."
                }, nonce);
            }

            // B2C 検証には UPN が必要 - 提供されていない場合は構築を試行
            if (string.IsNullOrEmpty(userPrincipalName))
            {
                _logger.LogWarning(
                    "[JIT Function] ⚠️ UPN not provided in payload | UserId: {UserId} | RequestId: {RequestId}",
                    userId, requestId);

                return await CreateExtensionResponseAsync(req, new JitMigrationResult
                {
                    ActionType = ResponseActionType.Block,
                    Title = "Configuration Error",
                    Message = "Unable to validate credentials. Please contact support."
                }, nonce);
            }

            // External ID UPN を B2C UPN に変換（逆変換）
            // 例: user@externalid.onmicrosoft.com → user@b2c.onmicrosoft.com
            var b2cUpn = TransformUpnForB2C(userPrincipalName);

            _logger.LogInformation(
                "[JIT Function] Transformed UPN for B2C validation | ExternalIdUPN: {ExternalIdUPN} | B2CUPN: {B2CUPN} | RequestId: {RequestId}",
                userPrincipalName, b2cUpn, requestId);

            // B2C または他のサードパーティ IDP への JIT 移行を実行
            _logger.LogInformation(
                "[JIT Function] Calling JIT migration service | UserId: {UserId} | B2C UPN: {UPN} | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                userId, b2cUpn, correlationId, requestId);

            var result = await _jitService.MigrateUserAsync(
                userId,
                b2cUpn,
                userPassword,
                correlationId,
                context.CancellationToken);

            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "[JIT Function] ✅ JIT migration completed | UserId: {UserId} | Action: {Action} | AlreadyMigrated: {AlreadyMigrated} | Duration: {Duration}ms | Nonce: {NonceStatus} | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                userId, result.ActionType, result.AlreadyMigrated, duration, !string.IsNullOrEmpty(nonce) ? "✓" : "✗", correlationId, requestId);

            return await CreateExtensionResponseAsync(req, result, nonce);
        }
        catch (JsonException jsonEx)
        {
            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            _logger.LogError(jsonEx,
                "[JIT Function] ❌ JSON parsing error | Duration: {Duration}ms | RequestId: {RequestId}",
                duration, requestId);

            return await CreateExtensionResponseAsync(req, new JitMigrationResult
            {
                ActionType = ResponseActionType.Block,
                Title = "Request Error",
                Message = "Unable to process authentication request."
            });
        }
        catch (Exception ex)
        {
            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            _logger.LogError(ex,
                "[JIT Function] ❌ EXCEPTION | Duration: {Duration}ms | RequestId: {RequestId} | ExceptionType: {ExceptionType}",
                duration, requestId, ex.GetType().Name);

            return await CreateExtensionResponseAsync(req, new JitMigrationResult
            {
                ActionType = ResponseActionType.Block,
                Title = "System Error",
                Message = "An error occurred during authentication. Please try again later."
            });
        }
    }

    /// <summary>
    /// カスタム認証拡張機能レスポンスを作成します。
    /// </summary>
    private static async Task<HttpResponseData> CreateExtensionResponseAsync(
        HttpRequestData req,
        JitMigrationResult result,
        string? nonce = null)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        var extResponse = new CustomAuthenticationExtensionResponse(
            result.ActionType,
            result.Title,
            result.Message);

        // 提供された場合、レスポンスに nonce を追加
        if (!string.IsNullOrEmpty(nonce))
        {
            extResponse.Data.Nonce = nonce;
        }

        var json = JsonSerializer.Serialize(extResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await response.WriteStringAsync(json);

        return response;
    }

    /// <summary>
    /// RSA 秘密キーを使用して External ID からの暗号化されたパスワード コンテキストを復号化します。
    /// 暗号化されたコンテキストは、External ID で構成された公開キーで暗号化された JWT トークンです。
    /// 秘密キーは Azure Key Vault またはインライン構成から取得されます。
    /// </summary>
    /// <param name="encryptedContext">External ID からの暗号化された JWT トークン</param>
    /// <param name="correlationId">ログ用の相関 ID</param>
    /// <param name="requestId">ログ用のリクエスト ID</param>
    /// <returns>（パスワード、nonce）を含むタプル</returns>
    private async Task<(string? password, string? nonce)> DecryptPasswordContext(
        string encryptedContext,
        string correlationId,
        string requestId)
    {
        try
        {
            _logger.LogDebug(
                "[JIT Function] Starting password decryption | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                correlationId, requestId);

            if (string.IsNullOrEmpty(encryptedContext))
            {
                _logger.LogError(
                    "[JIT Function] Encrypted context is null or empty | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                    correlationId, requestId);
                return (null, null);
            }

            // RSA 秘密キーを取得（キャッシュ、Key Vault、またはインライン構成から）
            var privateKeyPem = await GetPrivateKeyAsync(correlationId, requestId);

            if (string.IsNullOrEmpty(privateKeyPem))
            {
                _logger.LogError(
                    "[JIT Function] Failed to retrieve RSA private key | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                    correlationId, requestId);
                return (null, null);
            }

            // RSA インスタンスを作成し、秘密キーをインポート
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);

            _logger.LogDebug(
                "[JIT Function] RSA key imported, attempting JWT decryption | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                correlationId, requestId);

            // ステップ 1: RSA 秘密キーを使用して JWE を復号化
            string decryptedPayload = JWT.Decode(encryptedContext, rsa);

            if (string.IsNullOrEmpty(decryptedPayload))
            {
                _logger.LogError(
                    "[JIT Function] JWT decryption resulted in empty payload | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                    correlationId, requestId);
                return (null, null);
            }

            _logger.LogDebug(
                "[JIT Function] JWT decrypted successfully (payload length: {Length}), decoding inner JWT | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                decryptedPayload.Length, correlationId, requestId);

            // ステップ 2: 内部 JWS をデコード（暗号化なし、base64 のみ）
            string jsonPayload = JWT.Decode(decryptedPayload, null, JwsAlgorithm.none);

            if (string.IsNullOrEmpty(jsonPayload))
            {
                _logger.LogError(
                    "[JIT Function] Inner JWT decode resulted in empty payload | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                    correlationId, requestId);
                return (null, null);
            }

            _logger.LogDebug(
                "[JIT Function] Inner JWT decoded successfully, parsing JSON | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                correlationId, requestId);

            // 最終的な JSON ペイロードを解析
            var payloadDoc = JsonDocument.Parse(jsonPayload);
            var root = payloadDoc.RootElement;

            // ペイロードからパスワードと nonce を抽出
            string? password = root.TryGetProperty("user-password", out var pwdElement) 
                ? pwdElement.GetString() 
                : null;

            string? nonce = root.TryGetProperty("nonce", out var nonceElement) 
                ? nonceElement.GetString() 
                : null;

            _logger.LogInformation(
                "[JIT Function] Decryption complete | Password: {PasswordStatus}, Nonce: {NonceStatus} | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                !string.IsNullOrEmpty(password) ? "✓" : "✗",
                !string.IsNullOrEmpty(nonce) ? "✓" : "✗",
                correlationId, requestId);

            return (password, nonce);
        }
        catch (Jose.JoseException joseEx)
        {
            _logger.LogError(joseEx,
                "[JIT Function] Jose JWT library error during decryption | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                correlationId, requestId);
            return (null, null);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx,
                "[JIT Function] JSON parsing error after decryption | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                correlationId, requestId);
            return (null, null);
        }
        catch (CryptographicException cryptoEx)
        {
            _logger.LogError(cryptoEx,
                "[JIT Function] Cryptographic error (check private key format) | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                correlationId, requestId);
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[JIT Function] Unexpected error decrypting password context | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                correlationId, requestId);
            return (null, null);
        }
    }

    /// <summary>
    /// キャッシュ、Key Vault、またはインライン構成から RSA 秘密キーを取得します。
    /// Key Vault 呼び出しを削減するためにキャッシュを実装しています。
    /// </summary>
    private async Task<string?> GetPrivateKeyAsync(string correlationId, string requestId)
    {
        // キャッシュが有効で利用可能な場合、キャッシュされたキーを返す
        if (_jitOptions.CachePrivateKey && _cachedPrivateKey != null)
        {
            _logger.LogDebug(
                "[JIT Function] Using cached RSA private key | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                correlationId, requestId);
            return _cachedPrivateKey;
        }

        // 最初の読み込み時に同時 Key Vault 呼び出しを防ぐためにセマフォを使用
        await _keyLoadLock.WaitAsync();
        try
        {
            // ロック取得後に再確認
            if (_jitOptions.CachePrivateKey && _cachedPrivateKey != null)
            {
                return _cachedPrivateKey;
            }

            string? privateKey;

            if (_jitOptions.UseKeyVault)
            {
                // Azure Key Vault から取得
                _logger.LogInformation(
                    "[JIT Function] Retrieving RSA private key from Key Vault: {KeyName} | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                    _jitOptions.RsaKeyName, correlationId, requestId);

                privateKey = await _secretProvider!.GetSecretAsync(_jitOptions.RsaKeyName);

                if (string.IsNullOrEmpty(privateKey))
                {
                    _logger.LogError(
                        "[JIT Function] Key Vault returned empty private key for: {KeyName} | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                        _jitOptions.RsaKeyName, correlationId, requestId);
                    return null;
                }

                _logger.LogInformation(
                    "[JIT Function] ✓ RSA private key retrieved from Key Vault successfully | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                    correlationId, requestId);
            }
            else
            {
                // 構成からインライン キーを使用（ローカル開発専用）
                _logger.LogWarning(
                    "[JIT Function] Using inline RSA private key from configuration (local development mode) | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                    correlationId, requestId);

                privateKey = _jitOptions.InlineRsaPrivateKey;
            }

            // キャッシュが有効な場合、キーをキャッシュ
            if (_jitOptions.CachePrivateKey && !string.IsNullOrEmpty(privateKey))
            {
                _cachedPrivateKey = privateKey;
                _logger.LogDebug(
                    "[JIT Function] RSA private key cached in memory | CorrelationId: {CorrelationId} | RequestId: {RequestId}",
                    correlationId, requestId);
            }

            return privateKey;
        }
        finally
        {
            _keyLoadLock.Release();
        }
    }

    /// <summary>
    /// ドメインを置き換えて External ID UPN を B2C UPN に変換します。
    /// これは ImportOrchestrator.TransformUpnForExternalId の逆変換です。
    /// 例:
    ///   user@externalid.onmicrosoft.com → user@b2c.onmicrosoft.com
    ///   047102b7-221a-4fcf-9bf6-a179e37efd62@externalid.onmicrosoft.com → 047102b7-221a-4fcf-9bf6-a179e37efd62@b2c.onmicrosoft.com
    /// </summary>
    /// <param name="externalIdUpn">External ID からの UPN（External ID ドメイン付き）</param>
    /// <returns>ROPC 検証用の B2C ドメイン付き UPN</returns>
    private string TransformUpnForB2C(string externalIdUpn)
    {
        if (string.IsNullOrEmpty(externalIdUpn))
            return externalIdUpn;

        var atIndex = externalIdUpn.IndexOf('@');
        if (atIndex == -1)
            return externalIdUpn; // 無効な形式、そのまま返す

        // ローカル部分を抽出（@ より前のすべて）
        var localPart = externalIdUpn.Substring(0, atIndex);

        // External ID ドメインを B2C ドメインに置き換え
        var b2cUpn = $"{localPart}@{_migrationOptions.B2C.TenantDomain}";

        _logger.LogDebug(
            "[JIT Function] UPN domain transformation | Input: {Input} | Output: {Output}",
            externalIdUpn, b2cUpn);

        return b2cUpn;
    }
}
