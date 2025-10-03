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
using System.Text;
using Serilog;
using WebSockets.Common.Model;

namespace WebSockets.Common;

/// <summary>
/// Wrapper for common functions when dealing with websockets
/// </summary>
public static class WebsocketExtensions
{
    /// <summary>
    /// Write a message to the websocket connection serialized as a json string
    /// </summary>
    /// <param name="webSocket">The websocket to write to</param>
    /// <param name="envelopedMessage">The message envelope to send</param>
    /// <param name="privateKeyPem">The private key in PEM format</param>
    /// <param name="wrappingType">The wrapping type to use</param>
    /// <param name="key">The RSA key to use for encryption</param>
    /// <returns>The number of bytes written</returns>
    public static async Task<int> WriteMessage(this WebSocket webSocket, EnvelopedMessage envelopedMessage,
        WrappingType wrappingType, RSA? key = null)
    {
        if (webSocket is null)
            throw new WebSocketClientDisconnectedException();

        if (envelopedMessage is null)
            throw new InvalidOperationException("Cannot send a null message envelope");

        return await webSocket.WriteString(envelopedMessage.ToTransportFormat(wrappingType, key));

    }

    /// <summary>
    /// Writes a raw string message to the websocket connection
    /// </summary>
    /// <param name="webSocket">The websocket to write to</param>
    /// <param name="message">The message to send</param>
    /// <returns>The number of bytes written</returns>
    public static async Task<int> WriteString(this WebSocket webSocket, string message)
    {
        Log.Debug("SocketLevel Write: {message}", message);

        if (webSocket is null)
            throw new WebSocketClientDisconnectedException();

        try
        {
            await webSocket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true,
                CancellationToken.None);
            Log.Debug("SocketLevel Write Completed: {message}", message);
        }
        catch (System.Exception e)
        {
            Log.Error("Error writing to websocket {message}", e.Message);
            throw;
        }
        return message.Length;
    }

    /// <summary>
    /// Terminates the websocket connection with a PolicyViolation status
    /// </summary>
    /// <param name="webSocket">The websocket to terminate</param>
    /// <param name="message">The reason for termination</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task TerminateWithPolicyViolation(this WebSocket webSocket, string message)
    {
        try
        {
            Log.Warning($"Terminating websocket by PolicyViolation {message}");
            await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation,
                message,
                CancellationToken.None);
        }
        catch
        {
            // Ignored
        }
    }

    /// <summary>
    /// Terminates the websocket connection gracefully
    /// </summary>
    /// <param name="webSocket">The websocket to terminate</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task TerminateGracefully(this WebSocket webSocket)
    {
        try
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }
        catch
        {
            // Ignored
        }
    }
}

/// <summary>
/// Exception thrown when the websocket client is disconnected
/// </summary>
public class WebSocketClientDisconnectedException : System.Exception
{
}