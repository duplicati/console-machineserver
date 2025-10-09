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
using MachineService.Common.Exceptions;
using Serilog;
using WebSockets.Common;

namespace MachineService.Common.Model;

/// <summary>
/// Extension methods for the SocketState class to handle the receiving of messages 
/// in a simpler way.
/// </summary>
public static class SocketStateExtensions
{
    /// <summary>
    /// Continuously receives messages from the WebSocket and invokes the provided delegate
    /// for each complete message received.
    /// </summary>
    /// <param name="socketState">The socket state</param>
    /// <param name="messageDelegate">The delegate to invoke for each message</param>
    /// <param name="bufferSize">The buffer size for receiving messages</param>
    /// <param name="maxBytesInitialMessage">The maximum allowed size for the initial message</param>
    /// <param name="maxBytes">The maximum allowed size for subsequent messages</param>
    /// <param name="cancellationToken">A cancellation token to stop receiving</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task ReceiveMessages(this SocketState socketState,
        Func<WebSocketReceiveResult, byte[], Task> messageDelegate, int bufferSize,
        int maxBytesInitialMessage, int maxBytes, CancellationToken cancellationToken)
    {
        var currentMax = maxBytesInitialMessage;
        var started = DateTimeOffset.Now;
        try
        {
            while (socketState.WebSocket.State == WebSocketState.Open && cancellationToken.IsCancellationRequested == false)
            {
                var (result, data) = await socketState.WebSocket.ReceiveMessage(bufferSize, currentMax, cancellationToken);
                socketState.LastReceived = DateTimeOffset.Now;
                socketState.IncrementBytesReceived(data.Length);

                await messageDelegate(result, data.ToArray());
                currentMax = maxBytes; // After the initial message, allow larger messages
            }

            Log.Debug("WebSocket connection closed, duration: {Duration}, status: {CloseStatus} - {CloseStatusDescription}", DateTimeOffset.Now - started, socketState.WebSocket.CloseStatus, socketState.WebSocket.CloseStatusDescription);
        }
        catch (WebSocketException) when (socketState.WebSocket.State == WebSocketState.Aborted || socketState.WebSocket.State == WebSocketState.Closed)
        {
            // This is expected when the client simply disconnects, nothing to be done on our side
            // as cleanup of the object is implicit when it disposes itself.
            Log.Debug("WebSocket connection closed, duration: {Duration}, status: {CloseStatus} - {CloseStatusDescription}", DateTimeOffset.Now - started, socketState.WebSocket.CloseStatus, socketState.WebSocket.CloseStatusDescription);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the cancellation token is triggered, nothing to be done
            Log.Debug("WebSocket connection close requested, duration: {Duration}, status: {CloseStatus} - {CloseStatusDescription}", DateTimeOffset.Now - started, socketState.WebSocket.CloseStatus, socketState.WebSocket.CloseStatusDescription);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error during receiving process.");
            throw;
        }
    }

    /// <summary>
    /// Receives a complete message from the WebSocket, handling fragmentation and size limits.
    /// </summary>
    /// <param name="webSocket">The WebSocket instance</param>
    /// <param name="bufferSize">The buffer size for receiving messages</param>
    /// <param name="maxBytes">The maximum allowed size for the message</param>
    /// <param name="cancellationToken">A cancellation token to stop receiving</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task<(WebSocketReceiveResult, MemoryStream)> ReceiveMessage(this WebSocket webSocket, int bufferSize, int maxBytes, CancellationToken cancellationToken)
    {
        var readBuffer = new byte[bufferSize];
        var ms = new MemoryStream();

        try
        {
            WebSocketReceiveResult result;
            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(readBuffer), cancellationToken);
                ms.Write(readBuffer, 0, result.Count);
                if (ms.Length > maxBytes)
                    throw new PolicyViolationException(ErrorMessages.TooMuchDataWithoutAuthentication);
            } while (!result.EndOfMessage);

            ms.Position = 0;
            return (result, ms);
        }
        catch
        {
            ms.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Returns the expected wrapping type for the current connection state.
    /// </summary>
    /// <param name="socketState">The socketstate reference</param>
    /// <returns>WrappingType</returns>
    public static WrappingType InferWrapping(this SocketState socketState)
    {
        return socketState.ConnectionState switch
        {
            ConnectionState.ConnectedUnknown
            or ConnectionState.ConnectedPortalUnauthenticated
            or ConnectionState.ConnectedPortalAuthenticated
                => WrappingType.PlainText,

            ConnectionState.ConnectedAgentUnauthenticated
                => WrappingType.SignOnly,

            ConnectionState.ConnectedAgentAuthenticated
                => WrappingType.Encrypt,

            _ => WrappingType.PlainText
        };
    }
}