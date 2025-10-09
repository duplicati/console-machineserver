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
using MachineService.Common.Interfaces;
using MachineService.Common.Services;
using MachineService.Common.Util;
using MachineService.Server.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WebSockets.Common;

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
    public static void ConfigureWebSocketServer(this WebApplication app, Dictionary<string, Type> messageHandlers)
    {
        app.Use(async (context, next) =>
        {
            var settings = context.RequestServices.GetRequiredService<EnvironmentConfig>();
            var derivedConfig = context.RequestServices.GetRequiredService<DerivedConfig>();
            var connections = context.RequestServices.GetRequiredService<ConnectionListService>();
            var serverStatistics = context.RequestServices.GetRequiredService<ServerStatistics>();
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
                        case "/gateway":
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
                                    "/gateway" => ConnectionState.ConnectedGatewayUnauthenticated,
                                    _ => ConnectionState.ConnectedUnknown
                                };

                                socketState.ConnectedOn = DateTimeOffset.Now;
                                socketState.RemoteIpAddress = IpUtils.GetClientIp(context);

                                connections.Add(socketState);
                                serverStatistics.IncrementConnectionCount();

                                try
                                {
                                    var welcomeMessage = new EnvelopedMessage
                                    {
                                        From = settings.InstanceId,
                                        To = "unknown client",
                                        MessageId = Guid.NewGuid().ToString(),
                                        Type = MessageTypes.Welcome.ToString().ToLowerInvariant(),
                                        Payload = EnvelopedMessage.SerializePayload(new WelcomeMessage(
                                            PublicKeyHash: derivedConfig.PublicKeyHash,
                                            MachineName: settings.MachineName ?? "",
                                            ServerVersion: settings.GitVersion,
                                            Nonce: socketState.ConnectionState == ConnectionState.ConnectedGatewayUnauthenticated
                                             ? Convert.ToBase64String(socketState.NonceBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                                             : null,
                                            derivedConfig.AllowedProtocolVersions
                                        ))
                                    };

                                    await socketState.WriteMessage(welcomeMessage, WrappingType.PlainText);
                                    var messageLoop = ActivatorUtilities.CreateInstance<WebSocketMessageLoop>(
                                        context.RequestServices,
                                        socketState,
                                        messageHandlers.ToMessageHandlerFactory(context.RequestServices));
                                    await messageLoop.RunMessageLoop(context.RequestAborted);
                                }
                                finally
                                {
                                    // We are done with the connection, remove it from the list.
                                    connections.Remove(socketState);
                                }

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

    /// <summary>
    /// Converts a dictionary of message handler types into a factory function that can create instances of IMessageBehavior.
    /// </summary>
    /// <param name="messageHandlers">Dictionary mapping message types to their handler types</param>
    /// <param name="provider">The service provider for dependency injection</param>
    /// <returns>A factory function that takes a message type and returns an instance of IMessageBehavior or null if not found</returns>
    public static Func<string, IMessageBehavior?> ToMessageHandlerFactory(this Dictionary<string, Type> messageHandlers, IServiceProvider provider)
        => (type) => messageHandlers.ContainsKey(type)
            ? (IMessageBehavior)ActivatorUtilities.GetServiceOrCreateInstance(provider, messageHandlers[type])!
            : null;
}