// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using B2CMigrationKit.Core.Abstractions;
using B2CMigrationKit.Core.Configuration;
using B2CMigrationKit.Core.Services.Infrastructure;
using B2CMigrationKit.Core.Services.Observability;
using B2CMigrationKit.Core.Services.Orchestrators;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace B2CMigrationKit.Core.Extensions;

/// <summary>
/// DI コンテナーに Core サービスを登録するための拡張メソッド。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// すべての Core ライブラリ サービスを DI コンテナーに登録します。
    /// </summary>
    public static IServiceCollection AddMigrationKitCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 構成を登録
        services.Configure<MigrationOptions>(configuration.GetSection(MigrationOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection($"{MigrationOptions.SectionName}:Storage"));
        services.Configure<RetryOptions>(configuration.GetSection($"{MigrationOptions.SectionName}:Retry"));
        services.Configure<TelemetryOptions>(configuration.GetSection($"{MigrationOptions.SectionName}:Telemetry"));

        // Application Insights を登録（構成されている場合）
        var telemetryOptions = configuration.GetSection($"{MigrationOptions.SectionName}:Telemetry").Get<TelemetryOptions>();
        if (telemetryOptions?.UseApplicationInsights == true && !string.IsNullOrEmpty(telemetryOptions.ConnectionString))
        {
            var telemetryConfig = new TelemetryConfiguration
            {
                ConnectionString = telemetryOptions.ConnectionString
            };

            // 注: サンプリングは Application Insights ポータルまたはアダプティブ サンプリングで構成するのが最適です
            // プログラムによるサンプリングには、Microsoft.ApplicationInsights.WindowsServer NuGet パッケージを追加してください

            services.AddSingleton(telemetryConfig);
            services.AddSingleton<TelemetryClient>();
        }

        // インフラストラクチャ サービスを登録
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<IRsaKeyManager, RsaKeyManager>();

        // Key Vault を登録（構成されている場合）
        var kvOptions = configuration.GetSection("Migration:KeyVault").Get<KeyVaultOptions>();
        if (kvOptions != null && kvOptions.Enabled && !string.IsNullOrEmpty(kvOptions.VaultUri))
        {
            services.AddSingleton<ISecretProvider, SecretProvider>();
        }

        // Azure Storage クライアントを登録
        services.AddSingleton<IBlobStorageClient, BlobStorageClient>();
        services.AddSingleton<IQueueClient, QueueClient>();

        // B2C Credential Manager を登録
        services.AddSingleton<ICredentialManager>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MigrationOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<CredentialManager>>();
            var secretProvider = sp.GetService<ISecretProvider>();

            return new CredentialManager(
                options.B2C.AppRegistration,
                options.B2C.TenantId,
                secretProvider,
                logger);
        });

        // External ID Credential Manager を登録
        services.AddSingleton<ICredentialManager>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MigrationOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<CredentialManager>>();
            var secretProvider = sp.GetService<ISecretProvider>();

            return new CredentialManager(
                options.ExternalId.AppRegistration,
                options.ExternalId.TenantId,
                secretProvider,
                logger);
        });

        // Graph クライアントを登録
        services.AddHttpClient();

        services.AddScoped<IGraphClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MigrationOptions>>().Value;
            // B2C Credential Manager を取得（最初に登録されたもの）
            var credManager = sp.GetRequiredService<IEnumerable<ICredentialManager>>().First();
            var telemetry = sp.GetRequiredService<ITelemetryService>();
            var factoryLogger = sp.GetRequiredService<ILogger<GraphClientFactory>>();
            var clientLogger = sp.GetRequiredService<ILogger<GraphClient>>();

            var factory = new GraphClientFactory(credManager, factoryLogger, telemetry);
            var graphServiceClient = factory.CreateClient(options.B2C.Scopes);

            return new GraphClient(graphServiceClient, sp.GetRequiredService<IOptions<RetryOptions>>(), clientLogger, telemetry);
        });

        // 認証サービスを登録
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        // オーケストレーターとサービスを登録
        services.AddScoped<ExportOrchestrator>();

        // External ID Graph クライアントで ImportOrchestrator を登録
        services.AddScoped<ImportOrchestrator>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MigrationOptions>>().Value;
            // External ID Credential Manager を取得（2番目に登録されたもの）
            var credManager = sp.GetRequiredService<IEnumerable<ICredentialManager>>().Last();
            var telemetry = sp.GetRequiredService<ITelemetryService>();
            var factoryLogger = sp.GetRequiredService<ILogger<GraphClientFactory>>();
            var clientLogger = sp.GetRequiredService<ILogger<GraphClient>>();
            var retryOptions = sp.GetRequiredService<IOptions<RetryOptions>>();
            var blobClient = sp.GetRequiredService<IBlobStorageClient>();
            var orchestratorLogger = sp.GetRequiredService<ILogger<ImportOrchestrator>>();

            var factory = new GraphClientFactory(credManager, factoryLogger, telemetry);
            var graphServiceClient = factory.CreateClient(options.ExternalId.Scopes);
            var graphClient = new GraphClient(graphServiceClient, retryOptions, clientLogger, telemetry);

            return new ImportOrchestrator(graphClient, blobClient, telemetry, Options.Create(options), orchestratorLogger);
        });

        // External ID Graph クライアントで JitMigrationService を登録
        services.AddScoped<JitMigrationService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MigrationOptions>>().Value;
            var authService = sp.GetRequiredService<IAuthenticationService>();
            var telemetry = sp.GetRequiredService<ITelemetryService>();
            var logger = sp.GetRequiredService<ILogger<JitMigrationService>>();

            // External ID Credential Manager を作成
            var secretProvider = sp.GetService<ISecretProvider>();
            var credManagerLogger = sp.GetRequiredService<ILogger<CredentialManager>>();
            var externalIdCredManager = new CredentialManager(
                options.ExternalId.AppRegistration,
                options.ExternalId.TenantId,
                secretProvider,
                credManagerLogger);

            // External ID Graph クライアントを作成
            var factoryLogger = sp.GetRequiredService<ILogger<GraphClientFactory>>();
            var clientLogger = sp.GetRequiredService<ILogger<GraphClient>>();
            var retryOptions = sp.GetRequiredService<IOptions<RetryOptions>>();

            var factory = new GraphClientFactory(externalIdCredManager, factoryLogger, telemetry);
            var graphServiceClient = factory.CreateClient(options.ExternalId.Scopes);
            var externalIdGraphClient = new GraphClient(graphServiceClient, retryOptions, clientLogger, telemetry);

            return new JitMigrationService(authService, externalIdGraphClient, telemetry, Options.Create(options), logger);
        });

        services.AddScoped<ProfileSyncService>();

        return services;
    }

    /// <summary>
    /// B2C 固有の Graph クライアントを登録します。
    /// </summary>
    public static IServiceCollection AddB2CGraphClient(this IServiceCollection services)
    {
        services.AddScoped<IGraphClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MigrationOptions>>().Value;
            var credManager = sp.GetRequiredService<IEnumerable<ICredentialManager>>()
                .First(); // B2C Credential Manager
            var telemetry = sp.GetRequiredService<ITelemetryService>();
            var factoryLogger = sp.GetRequiredService<ILogger<GraphClientFactory>>();
            var clientLogger = sp.GetRequiredService<ILogger<GraphClient>>();
            var retryOptions = Options.Create(options.Retry);

            var factory = new GraphClientFactory(credManager, factoryLogger, telemetry);
            var graphServiceClient = factory.CreateClient(options.B2C.Scopes);

            return new GraphClient(graphServiceClient, retryOptions, clientLogger, telemetry);
        });

        return services;
    }

    /// <summary>
    /// External ID 固有の Graph クライアントを登録します。
    /// </summary>
    public static IServiceCollection AddExternalIdGraphClient(this IServiceCollection services)
    {
        services.AddScoped<IGraphClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MigrationOptions>>().Value;
            var credManager = sp.GetRequiredService<IEnumerable<ICredentialManager>>()
                .Last(); // External ID Credential Manager
            var telemetry = sp.GetRequiredService<ITelemetryService>();
            var factoryLogger = sp.GetRequiredService<ILogger<GraphClientFactory>>();
            var clientLogger = sp.GetRequiredService<ILogger<GraphClient>>();
            var retryOptions = Options.Create(options.Retry);

            var factory = new GraphClientFactory(credManager, factoryLogger, telemetry);
            var graphServiceClient = factory.CreateClient(options.ExternalId.Scopes);

            return new GraphClient(graphServiceClient, retryOptions, clientLogger, telemetry);
        });

        return services;
    }
}
