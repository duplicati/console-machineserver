// Copyright (c) 2025 Duplicati Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using System.Security.Cryptography;

namespace MachineService.Common.Model;

/// <summary>
/// Record for holding the private/publick key configuration of the server,
/// and generating public key hash.
/// </summary>
/// <param name="PublicKeyHash">Public Key hash of the private key</param>
/// <param name="PrivateKey">Private Key</param>
/// <param name="Expires">Expiration date of the public key</param>
/// <param name="AllowedProtocolVersions">Array of allowed protocol versions supported</param>
public sealed record DerivedConfig(
    string PublicKeyHash,
    RSA PrivateKey,
    DateTimeOffset Expires,
    IEnumerable<int> AllowedProtocolVersions)
{

    /// <summary>
    /// Initialized a new DerivedConfig record from a private key PEM string(PEM format)
    /// </summary>
    /// <param name="privateKeyPem">Private key (PEM format string)</param>
    /// <param name="expires">Expiration date of the public key</param>
    /// <returns></returns>
    public static DerivedConfig Create(string privateKeyPem, DateTimeOffset expires)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        return new DerivedConfig(
            PublicKeyHash: ComputeKeyHash(rsa),
            PrivateKey: rsa,
            Expires: expires,
            AllowedProtocolVersions: [1]);
    }

    /// <summary>
    /// Computes the public key hash from the private key
    /// </summary>
    /// <param name="privateKey">The private key</param>
    /// <returns>The public key hash</returns>
    private static string ComputeKeyHash(RSA privateKey)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(privateKey.ExportRSAPublicKey());
        return Convert.ToBase64String(hash)[0..16];
    }
}



