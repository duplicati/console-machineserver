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
using System.Net.WebSockets;
using System.Security.Cryptography;
using WebSockets.Common;

namespace MachineService.Common.Model;

/// <summary>
/// Representes the state of a connection to machineservices, including the websocket reference
/// </summary>
public record SocketState : IDisposable
{
    /// <summary>
    /// Each socketstate gets a unique connectionID, used merely for logging purposes
    /// </summary>
    public Guid ConnectionId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The underlying WebSocket connection for this client.
    /// </summary>
    public required WebSocket WebSocket { get; set; }

    /// <summary>
    /// Indicates whether the client has been authenticated.
    /// </summary>
    public bool Authenticated { get; set; }

    /// <summary>
    /// Indicates whether the handshake process is complete. True when ClientPublicKey is set.
    /// </summary>
    public bool HandshakeComplete => ClientPublicKey != null;

    /// <summary>
    /// The version of the protocol being used for communication.
    /// </summary>
    public int ProtocolVersion { get; set; }

    /// <summary>
    /// The client's public RSA key used for secure communication.
    /// </summary>
    public RSA? ClientPublicKey { get; set; }

    /// <summary>
    /// The version identifier of the connected client software.
    /// </summary>
    public string? ClientVersion { get; set; }

    /// <summary>
    /// Timestamp when the client established the connection.
    /// </summary>
    public DateTimeOffset ConnectedOn { get; set; }

    /// <summary>
    /// Timestamp of the last message received from the client.
    /// </summary>
    public DateTimeOffset LastReceived { get; set; }

    /// <summary>
    /// Timestamp of the last message sent to the client.
    /// </summary>
    public DateTimeOffset LastSent { get; set; }

    /// <summary>
    /// Unique identifier for this client connection.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Identifier of the organization this client belongs to, if any.
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Expiration time of the client's authentication token.
    /// </summary>
    public DateTimeOffset? TokenExpiration { get; set; }

    /// <summary>
    /// Identifier of the registered agent associated with this client, if any.
    /// </summary>
    public string? RegisteredAgentId { get; set; }

    /// <summary>
    /// The remote IP address of the connected client.
    /// </summary>
    public string? RemoteIpAddress { get; set; }

    /// <summary>
    /// Represents the type of connection, if its Agent, Portal (and in future gateway)
    /// </summary>
    public ConnectionType Type { get; set; }

    /// <summary>
    /// Statistics - Total bytes received from this connection
    /// </summary>
    public long BytesReceived;
    /// <summary>
    /// Statistics - Total bytes sent to this connection
    /// </summary>
    public long BytesSent;

    /// <summary>
    /// Represents the current state of the connection, if its connected, authenticated, etc.
    ///
    /// This is logical states, not the actual websocket state.
    /// </summary>
    public ConnectionState ConnectionState { get; set; } = ConnectionState.ConnectedUnknown;

    /// <summary>
    /// Increment the bytes received statistics. If the counter is at the maximum value, it will reset to 0 to avoid overflow.
    /// </summary>
    /// <param name="bytesReceived">Bytes to increment the counter by</param>
    public void IncrementBytesReceived(Int64 bytesReceived)
    {
        if (BytesReceived == Int64.MaxValue)
            BytesReceived = 0;

        Interlocked.Add(ref BytesReceived, bytesReceived);
    }

    /// <summary>
    /// Increment the bytes sent statistics. If the counter is at the maximum value, it will reset to 0 to avoid overflow.
    /// </summary>
    /// <param name="bytesSent">Bytes to increment the counter by</param>
    private void IncrementBytesSent(Int64 bytesSent)
    {
        if (BytesSent == Int64.MaxValue)
            BytesSent = 0;

        Interlocked.Add(ref BytesSent, bytesSent);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            WebSocket?.Dispose();
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    /// Writes an envelope message to socket using the correct wrapping type,
    /// For portal connection we will always talk in plaintext, every other type,
    /// it might be either encrypted or signedOnly depending on the phase of the connection.
    /// </summary>
    /// <param name="message">The message envelope</param>
    /// <param name="derivedConfig">Encryption keys reference</param>
    /// <returns>The number of bytes sent</returns>
    public Task<int> WriteMessage(EnvelopedMessage message, DerivedConfig derivedConfig)
    {
        // Update the last sent time statistics
        LastSent = DateTimeOffset.Now;

        int bytesSent;

        if (new List<ConnectionState>
            {
                ConnectionState.ConnectedAgentUnauthenticated,
                ConnectionState.ConnectedPortalUnauthenticated,
                ConnectionState.ConnectedPortalAuthenticated
            }.Contains(ConnectionState))
        {
            bytesSent = WebSocket.WriteMessage(message,
                WrappingType.PlainText, derivedConfig.PrivateKey).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        else
        {
            bytesSent = WebSocket.WriteMessage(
                message,
                HandshakeComplete ? WrappingType.Encrypt : WrappingType.SignOnly,
                HandshakeComplete ? ClientPublicKey : derivedConfig.PrivateKey
            ).ConfigureAwait(false).GetAwaiter().GetResult(); ;
        }

        // Increment bytes sent statistics
        IncrementBytesSent(bytesSent);

        return Task.FromResult(bytesSent);
    }

    /// <summary>
    /// Writes an envelope message to the socket using the specified wrapping type and optional RSA key.
    /// </summary>
    /// <param name="envelopedMessage">The message envelope</param>
    /// <param name="wrappingType">The wrapping type to use</param>
    /// <param name="key">The RSA key to use for encryption or signing</param>
    /// <returns>The number of bytes sent</returns>
    public async Task<int> WriteMessage(EnvelopedMessage envelopedMessage, WrappingType wrappingType, RSA? key = null)
    {
        LastSent = DateTimeOffset.Now;

        var bytesSent = await WebSocket.WriteString(envelopedMessage.ToTransportFormat(wrappingType, key));

        // Increment bytes sent statistics
        IncrementBytesSent(bytesSent);

        return bytesSent;
    }
}