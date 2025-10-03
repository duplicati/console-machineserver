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
using MachineService.Common.Services;
using MachineService.Common.Util;
using MachineService.Server.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WebSockets.Common;
using WebSockets.Common.Exception;

namespace MachineService.Common;

/// <summary>
/// This extension class provides a way to configure a WebSocket server in a WebApplication,
/// specifically designed to handle messages from the MachineService clients and neighboring gateway services.
/// </summary>
public static class WebSocketServerExtensions
{
    /// <summary>
    /// Configures the WebApplication to handle WebSocket connections.
    /// </summary>
    /// <param name="app">The webapplication object to hook onto</param>
    /// <param name="messageHandlers">Array of message handlers, that contains actions to be performed by messagetype</param>
    /// <param name="serverStatistics">Statistics gatherer reference</param>
    public static void ConfigureWebSocketServer(this WebApplication app, Dictionary<string, Type> messageHandlers, ServerStatistics serverStatistics)
    {
        app.Use(async (context, next) =>
        {
            var settings = context.RequestServices.GetService<EnvironmentConfig>();
            var derivedConfig = context.RequestServices.GetService<DerivedConfig>();
            var connections = context.RequestServices.GetService<ConnectionListService>();
            var onAfterDisconnect = context.RequestServices.GetService<IAfterDisconnectBehavior>();

            if (settings == null || derivedConfig == null || connections == null)
            {
                Log.Error("No valid configuration was present therefore cannot process requests at this time.");
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
            else
            {
                try
                {
                    string route = context.Request.Path.ToString().ToLower();
                    // Process the request.
                    switch (route)
                    {
                        case "/":
                            if (string.IsNullOrWhiteSpace(settings.RedirectUrl))
                                context.Response.StatusCode = StatusCodes.Status404NotFound;
                            else
                                context.Response.Redirect(settings.RedirectUrl);
                            break;
                        case "/health":
                            // This is merely for a heartbeat/check on deployment. 
                            context.Response.StatusCode = StatusCodes.Status200OK;
                            break;
                        case "/agent":
                        case "/portal":
                            if (context.WebSockets.IsWebSocketRequest)
                            {
                                using var socketState = new SocketState()
                                {
                                    WebSocket = await context.WebSockets.AcceptWebSocketAsync()
                                };

                                // At this point we infer the connectionstate by the route, which will by consequence 
                                // limit what the client can do, so if it points itself to the wrong route, it will not
                                // be able to send commands that don't belong to that state/client type.
                                // We however do not set the socket Type yet.

                                socketState.ConnectionState = route switch
                                {
                                    "/portal" => ConnectionState.ConnectedPortalUnauthenticated,
                                    "/agent" => ConnectionState.ConnectedAgentUnauthenticated,
                                    _ => ConnectionState.ConnectedUnknown
                                };

                                socketState.ConnectedOn = DateTimeOffset.Now;
                                socketState.RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString();

                                connections.Add(socketState);

                                serverStatistics.IncrementConnectionCount();

                                var welcomeMessage = new EnvelopedMessage
                                {
                                    From = settings.MachineName,
                                    To = "unknown client",
                                    MessageId = Guid.NewGuid().ToString(),
                                    Type = MessageTypes.Welcome.ToString().ToLowerInvariant(),
                                    Payload = EnvelopedMessage.SerializePayload(new WelcomeMessage(
                                        PublicKeyHash: derivedConfig.PublicKeyHash,
                                        MachineName: settings.MachineName ?? "",
                                        ServerVersion: settings.GitVersion,
                                        derivedConfig.AllowedProtocolVersions
                                    ))
                                };

                                await socketState.WebSocket.WriteMessage(welcomeMessage, WrappingType.PlainText);

                                await socketState.ReceiveMessages(async (result, buffer) =>
                                    {
                                        serverStatistics.IncrementMessagesReceivedCount();

                                        if (result.MessageType == WebSocketMessageType.Text)
                                        {

                                            try
                                            {
                                                var derivedConfig = context.RequestServices.GetRequiredService<DerivedConfig>();

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
                                                        From = settings.MachineName,
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
                                                    if (messageHandlers.TryGetValue(requestMessage.Type, out var handlerType))
                                                    {
                                                        try
                                                        {
                                                            // using var reqContext = context.RequestServices.CreateAsyncScope();
                                                            var req = (IMessageBehavior)ActivatorUtilities
                                                                .GetServiceOrCreateInstance(context.RequestServices,
                                                                    handlerType);
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
                                                        Log.Error($"No handler found for message type {requestMessage.Type}");
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
                                                connections.Remove(socketState);

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
                                            connections.Remove(socketState);
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
                                    },
                                    settings.WebsocketReceiveBufferSize
                                );

                                // We are done with the connection, remove it from the list.
                                connections.Remove(socketState);

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
                            else
                            {
                                Log.Warning("NON WEBSOCKET REQUEST RECEIVED, DENYING IT WITH HTTP 400");
                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                await context.Response.WriteAsync("Only websocket clients are allowed");

                            }

                            break;
                        default:
                            // This exists so that if we any other route is added to the application, it will be processed.
                            // In effect, the swagger routes.
                            await next(context);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e,
                        "An error occurred while processing the request, will try to return 500, and proceed.");

                    try
                    {
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        });
    }
}