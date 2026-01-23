// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace B2CMigrationKit.Core.Abstractions;

/// <summary>
/// JIT 認証用の RSA 公開鍵エクスポートおよび検証ユーティリティを提供します。
/// RSA キーペアは Azure Key Vault で生成・保存され、秘密鍵は Key Vault から外部に出ることはありません。
/// このインターフェースは External ID 構成用に公開鍵をエクスポートするのに役立ちます。
/// </summary>
public interface IRsaKeyManager
{
    /// <summary>
    /// RSA インスタンスから PEM 形式で公開鍵をエクスポートします。
    /// External ID での手動構成用に公開鍵を表示するために使用されます。
    /// </summary>
    /// <param name="rsa">公開鍵を含む RSA インスタンス</param>
    /// <returns>PEM エンコードされた文字列としての公開鍵</returns>
    string ExportPublicKeyPem(System.Security.Cryptography.RSA rsa);

    /// <summary>
    /// External ID 構成用に JWK（JSON Web Key）形式で公開鍵をエクスポートします。
    /// JWK 形式は External ID カスタム認証拡張機能でのペイロード暗号化に必要です。
    /// </summary>
    /// <param name="rsa">公開鍵を含む RSA インスタンス</param>
    /// <returns>JWK JSON 文字列としての公開鍵</returns>
    string ExportPublicKeyJwk(System.Security.Cryptography.RSA rsa);

    /// <summary>
    /// 暗号化/復号化テストを実行して、公開鍵/秘密鍵ペアが一致することを検証します。
    /// </summary>
    /// <param name="privateKeyPem">PEM 形式の秘密鍵</param>
    /// <param name="publicKeyPem">PEM 形式の公開鍵</param>
    /// <returns>キーが有効なペアの場合は true、それ以外は false</returns>
    bool ValidateKeyPair(string privateKeyPem, string publicKeyPem);

    /// <summary>
    /// PEM 形式から RSA 公開鍵をインポートします。
    /// 検証のために Key Vault からエクスポートされた公開鍵を読み込むために使用されます。
    /// </summary>
    /// <param name="publicKeyPem">PEM 形式の公開鍵</param>
    /// <returns>インポートされた公開鍵を持つ RSA インスタンス</returns>
    System.Security.Cryptography.RSA ImportPublicKeyFromPem(string publicKeyPem);

    /// <summary>
    /// 開発/テスト目的のみでローカル RSA キーペアを生成します。
    /// 本番環境: Azure Key Vault を使用してキーを生成してください（New-AzKeyVaultKey）。
    /// </summary>
    /// <param name="keySizeInBits">ビット単位のキーサイズ（2048 または 4096 を推奨）</param>
    /// <returns>キーペアを含む RSA インスタンス</returns>
    System.Security.Cryptography.RSA GenerateKeyPairForTesting(int keySizeInBits = 2048);
}
