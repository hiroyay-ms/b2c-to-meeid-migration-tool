// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using B2CMigrationKit.Core.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();

        // 移行キット コア サービスを登録
        services.AddMigrationKitCore(context.Configuration);
    })
    .Build();

await host.RunAsync();
