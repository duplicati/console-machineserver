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
using System.Text;
using System.Text.Json;
using Jose;
using WebSockets.Common.Exception;

namespace WebSockets.Common.Model;


/// <summary>
/// The base message envelope for all communication on remote management features.
/// </summary>
public class EnvelopedMessage
{
    /// <summary>
    /// The sender of the message
    /// </summary>
    public string? From { get; set; }
    /// <summary>
    /// The recipient of the message
    /// </summary>
    public string? To { get; set; }
    /// <summary>
    /// The type of the message, e.g. AuthMessage, ControlCommandMessage etc.
    /// </summary>
    public string? Type { get; set; }
    /// <summary>
    /// The message identifier, used for correlating requests and responses
    /// </summary>
    public string? MessageId { get; set; }
    /// <summary>
    /// The payload of the message, typically a serialized JSON object
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// Optional error message, used in responses to indicate an error
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// JSON serializer options to use for serializing and deserializing messages
    /// </summary>
    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>
    /// Creates an EnvelopedMessage from raw bytes, automatically detecting the wrapping type if not provided.
    /// </summary>
    /// <param name="rawMessageBytes">The raw message bytes</param>
    /// <param name="key">Key material, in PEM format</param>
    /// <param name="wrappingType">The wrapping type to use</param>
    /// <returns>The deserialized EnvelopedMessage</returns>
    public static EnvelopedMessage? FromBytes(byte[] rawMessageBytes, string key, WrappingType wrappingType)
        => wrappingType switch
        {
            WrappingType.PlainText => EnvelopedMessage.Deserialize(Encoding.UTF8.GetString(rawMessageBytes)),
            WrappingType.SignOnly => FromJweString(Encoding.UTF8.GetString(rawMessageBytes), key, false),
            WrappingType.Encrypt => FromJweString(Encoding.UTF8.GetString(rawMessageBytes), key, true),
            _ => throw new ArgumentOutOfRangeException()
        };

    /// <summary>
    /// This method decodes both JWE and JWT messages, what distinguises between them is the parameter
    /// isPrivateKey. If isPrivateKey is true, it will decode a JWE message, otherwise it will decode a JWT message.
    /// </summary>
    /// <param name="rawMessage">The raw JWT/JWE Token</param>
    /// <param name="key">Key material, in PEM format</param>
    /// <param name="isPrivateKey">If isPrivateKey is true, it will decode a JWE message, otherwise it will decode a JWT message.</param>
    /// <returns>>The deserialized EnvelopedMessage</returns>
    /// <exception cref="EnvelopeJsonParsingException">Private/public keys don't match or message is otherwise corrupted</exception>
    public static EnvelopedMessage? FromJweString(string rawMessage, string key, bool isPrivateKey)
    {
        try
        {
            RSA rsaKeys = RSA.Create();
            rsaKeys.ImportFromPem(key);
            var jwk = new Jwk(rsaKeys, isPrivateKey);
            return Deserialize(JWT.Decode(rawMessage, jwk));
        }
        catch (JoseException jex)
        {
            throw new JwtOrJweDecryptionException("Failed to decrypt JWE or JTW", jex);
        }
        // Specifically we don't catch the potential EnvelopeJsonParsingException from Deserialize here. 
        // Because we want that to be treated on the caller side where flow control is more appropriate.
    }

    /// <summary>
    /// Serializes the EnvelopedMessage to a JSON string
    /// </summary>
    /// <returns>The JSON string representation of the EnvelopedMessage</returns>
    public string Serialize()
        => JsonSerializer.Serialize(this, JsonSerializerOptions);

    /// <summary>
    /// Deserializes a JSON string to an EnvelopedMessage
    /// </summary>
    /// <param name="json">The JSON string</param>
    /// <returns>The deserialized EnvelopedMessage</returns>
    public static EnvelopedMessage Deserialize(string json)
        => JsonSerializer.Deserialize<EnvelopedMessage>(json, JsonSerializerOptions)
            ?? throw new EnvelopeJsonParsingException("Invalid Json message");

    /// <summary>
    /// Serializes a payload object to a JSON string
    /// </summary>
    /// <typeparam name="T">The type of the payload object</typeparam>
    /// <param name="obj">The payload object</param>
    /// <returns>The JSON string representation of the payload object</returns>
    public static string SerializePayload<T>(T obj)
        => JsonSerializer.Serialize(obj, JsonSerializerOptions);

    /// <summary>
    /// Deserializes the Payload property to an object of type T
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <returns>The deserialized object of type T, or default if Payload is null</returns>
    public T? DeserializePayload<T>()
    {
        if (Payload == null)
            return default;

        return JsonSerializer.Deserialize<T>(Payload, JsonSerializerOptions);
    }

    /// <summary>
    /// Converts the EnvelopedMessage to the specified transport format, either plain text, signed JWT, or encrypted JWE.
    /// </summary>
    /// <param name="wrappingType">The wrapping type to use</param>
    /// <param name="key">The RSA key to use for signing or encryption</param>
    /// <returns>The message in the specified transport format</returns>
    public string ToTransportFormat(WrappingType wrappingType, RSA? key)
    {
        switch (wrappingType)
        {
            case WrappingType.PlainText:
                return Serialize();
            case WrappingType.SignOnly:
                if (key == null)
                    throw new System.Exception("Must supply a key for signing");
                return JWT.Encode(
                    this.Serialize(),
                    new Jwk(key, false), JwsAlgorithm.RS256,
                    extraHeaders: new Dictionary<string, object>()
                    {
                        { "encrypted", "false" },
                        { "version", "1" }
                    }
                );
            case WrappingType.Encrypt:
                if (key == null)
                    throw new System.Exception("Must supply a key for encryption");
                return JWT.Encode(
                    this.Serialize(),
                    new Jwk(key, false), // False to ensure we encrypt with publickey only. 
                    JweAlgorithm.RSA_OAEP_256,
                    JweEncryption.A256CBC_HS512,
                    extraHeaders: new Dictionary<string, object>()
                    {
                        { "encrypted", "true" },
                        { "version", "1" }
                    }
                );
            default:
                throw new System.Exception("Cannot determine wrapping type");
        }
    }
}