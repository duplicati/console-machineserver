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
using System.Text;
using MachineService.Common.Exceptions;
using MachineService.Common.Interfaces;
using MachineService.Common.Util;
using MachineService.Server.Model;
using Serilog;
using WebSockets.Common;
using WebSockets.Common.Exception;

namespace MachineService.Common;

/// <summary>
/// WebSocket message loop handler
/// </summary>
/// <param name="serverStatistics">The server statistics instance</param>
/// <param name="settings">The environment settings</param>
/// <param name="derivedConfig">The derived configuration</param>
/// <param name="onAfterDisconnect">The behavior to execute after disconnect</param>
/// <param name="socketState">The current socket state</param>
/// <param name="messageHandlerFactory">The factory to create message handlers</param>
public class WebSocketMessageLoop(ServerStatistics serverStatistics,
    EnvironmentConfig settings,
    DerivedConfig derivedConfig,
    IAfterDisconnectBehavior? onAfterDisconnect,
    SocketState socketState,
    Func<string, IMessageBehavior?> messageHandlerFactory)
{
    /// <summary>
    /// Runs the message loop, receiving messages and processing them.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to stop the loop</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task RunMessageLoop(CancellationToken cancellationToken)
    {
        try
        {
            await socketState.ReceiveMessages(
                RunMessageLoopInner,
                settings.WebsocketReceiveBufferSize,
                settings.MaxBytesBeforeAuthentication,
                settings.MaxMessageSize,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // This is expected when the service is stopping, nothing to be done.
        }
        catch (WebSocketException wse)
        {
            Log.Debug(wse, "WebSocket exception in message loop for client {ClientId}", socketState.ClientId);
        }
        catch (PolicyViolationException pve)
        {
            Log.Warning(pve, "Policy violation in message loop for client {ClientId}", socketState.ClientId);
            try
            {
                await socketState.WebSocket.TerminateWithPolicyViolation(pve.Message);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error while terminating websocket with policy violation for client {ClientId}", socketState.ClientId);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error in message loop for client {ClientId}", socketState.ClientId);
        }
    }

    /// <summary>
    /// Processes a single received message
    /// </summary>
    /// <param name="result">The WebSocket receive result</param>
    /// <param name="buffer">The received message buffer</param>
    /// <returns>>A task representing the asynchronous operation</returns>
    private async Task RunMessageLoopInner(WebSocketReceiveResult result, byte[] buffer)
    {
        serverStatistics.IncrementMessagesReceivedCount();

        if (result.MessageType == WebSocketMessageType.Text)
        {
            try
            {
                if (socketState.Authenticated == false &&
                    socketState.BytesReceived > settings.MaxBytesBeforeAuthentication)
                {
                    var message = ErrorMessages.TooMuchDataWithoutAuthentication;
                    Log.Warning(message);

                    throw new PolicyViolationException(message);
                }

                if (socketState.Authenticated &&
                    socketState.TokenExpiration < DateTimeOffset.Now)
                {
                    var message = $"Token expired on {socketState.TokenExpiration}";
                    Log.Warning(message);
                    var warningMessage = new EnvelopedMessage()
                    {
                        From = settings.InstanceId,
                        To = socketState.ClientId,
                        MessageId = Guid.NewGuid().ToString(),
                        Type = MessageTypes.Warning.ToString().ToLowerInvariant(),
                        ErrorMessage = ErrorMessages.TokenExpired
                    };

                    await socketState.WriteMessage(warningMessage, derivedConfig);

                    throw new PolicyViolationException(message);
                }

                // We have a message, lets unwrap and process.

                Log.Debug(
                    $"Message Received on Wire from {socketState.ClientId} {Encoding.UTF8.GetString(buffer)}");
                // If messages arrive encrypted, we will need the privatekey to decrypt it.
                EnvelopedMessage? requestMessage = null;

                Log.Debug(
                    $"{socketState.ClientId}\tState is: {socketState.ConnectionState}\tInferred wrapping: {socketState.InferWrapping()}");

                try
                {
                    requestMessage = EnvelopedMessage.FromBytes(buffer,
                        Base64PemReader.Read(settings.MachineServerPrivate!), socketState.InferWrapping());
                }
                catch (JwtOrJweDecryptionException dex)
                {
                    Log.Warning(dex,
                        $"{ErrorMessages.InvalidConnectionStateForAuthentication}. State was: {socketState.ConnectionState} and inferred wrapping was {socketState.InferWrapping()}");

                    throw new PolicyViolationException(ErrorMessages.InvalidConnectionStateForAuthentication);
                }
                catch (EnvelopeJsonParsingException e)
                {
                    Log.Warning(e,
                        $"{ErrorMessages.MalformedEnvelope} {Encoding.UTF8.GetString(buffer)}");

                    throw new PolicyViolationException(ErrorMessages.MalformedEnvelope);
                }

                Log.Debug(
                    $"Message {requestMessage?.Type} received from: {requestMessage?.From} {requestMessage?.Serialize()}");

                if (requestMessage is not null && requestMessage.Type is not null)
                {
                    try
                    {
                        var req = messageHandlerFactory(requestMessage.Type);
                        if (req == null)
                            Log.Error($"Handler for message type {requestMessage.Type} not found");
                        else
                            await req.ExecuteAsync(socketState, requestMessage);
                    }
                    catch (PolicyViolationException)
                    {
                        // In this case we rethrow, as it will be appropriatelly treated in the flow,
                        // specifically in the catch block of the ReceiveMessages method.
                        throw;
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, $"Failed to process message {requestMessage.Type}");
                    }
                }
                else
                {
                    Log.Warning($"Message received was null or had no type, buffer to analyze {Encoding.UTF8.GetString(buffer)}");
                }
            }
            catch (PolicyViolationException pev)
            {
                Log.Warning(pev, $"Policy Violation, closing websocket ClientID: {socketState.ClientId} Bytes Received: {socketState.BytesReceived} Bytes Sent: {socketState.BytesSent}");
                await socketState.WebSocket.TerminateWithPolicyViolation(pev.Message);

                await socketState.WebSocket.TerminateWithPolicyViolation(
                    pev.Message);

                try
                {
                    if (onAfterDisconnect is not null)
                        await onAfterDisconnect.ExecuteAsync(socketState);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error while calling the AfterDisconnect delegate");
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "An error occurred while processing the message");
            }
        }
        else if (result.MessageType == WebSocketMessageType.Binary)
        {
            // We are not supporting it now.
            Log.Warning("Received a binary format message on websocket, ignoring it");
        }
        else if (result.MessageType == WebSocketMessageType.Close ||
                 socketState.WebSocket.State == WebSocketState.Aborted)
        {
            await socketState.WebSocket.TerminateGracefully();
            try
            {
                if (onAfterDisconnect is not null)
                    await onAfterDisconnect.ExecuteAsync(socketState);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error while calling the AfterDisconnect delegate");
            }
        }
    }
}
