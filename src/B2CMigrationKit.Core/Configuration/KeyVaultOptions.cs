// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel.DataAnnotations;

namespace B2CMigrationKit.Core.Configuration;

/// <summary>
/// Azure Key Vault の構成オプション。
/// </summary>
public class KeyVaultOptions
{
    /// <summary>
    /// Key Vault 統合が有効かどうかを取得または設定します（既定値: false）。
    /// false の場合、Key Vault サービスは登録されず、インライン シークレットが使用されます。
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Key Vault の URI（例: https://myvault.vault.azure.net/）を取得または設定します。
    /// Enabled = true の場合に必須です。
    /// </summary>
    [Url]
    public string? VaultUri { get; set; }

    /// <summary>
    /// 認証にマネージド ID を使用するかどうかを取得または設定します（既定値: true）。
    /// true の場合、Azure マネージド ID を使用します（本番環境で推奨）。
    /// false の場合、DefaultAzureCredential（Visual Studio、Azure CLI など）にフォールバックします。
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// シークレットのキャッシュ期間（分）を取得または設定します（既定値: 60）。
    /// </summary>
    [Range(1, 1440)]
    public int SecretCacheDurationMinutes { get; set; } = 60;
}
