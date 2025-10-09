// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using System.Net.WebSockets;
using MachineService.Common;
using MachineService.Common.Model;
using MachineService.Common.Services;
using MachineService.GatewayClient.Behaviours;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WebSockets.Common.Model;

namespace MachineService.GatewayClient.Services;

/// <summary>
/// Background service that maintains connections to configured gateway servers.
/// </summary>
/// <param name="config">Injected configuration</param>
/// <param name="serviceScopeFactory">Injected service scope factory</param>
/// <param name="gatewayConnectionList">Injected gateway connection list</param>
public class GatewayConnectionKeeper(EnvironmentConfig config, IServiceScopeFactory serviceScopeFactory, GatewayConnectionList gatewayConnectionList) : BackgroundService
{
    /// <summary>
    /// Interval to reconnect to the gateway after failure
    /// </summary>
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(30);
    /// <summary>
    /// Interval to send pings to keep the connection alive
    /// </summary>
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = config.GatewayServers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(gatewayServer => Task.Run(() => ConnectToGatewayLoop(gatewayServer, stoppingToken), stoppingToken))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Keeprs a connection to a single gateway server
    /// and will reconnect if disconnected
    /// </summary>
    /// <param name="gatewayServer">The gateway server URL</param>
    /// <param name="stoppingToken">Cancellation token to stop the loop</param>
    /// <returns>A task that represents the connection loop</returns>
    private async Task ConnectToGatewayLoop(string gatewayServer, CancellationToken stoppingToken)
    {
        var failedAttempts = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ConnectToGateway(gatewayServer, stoppingToken) == ConnectionState.ConnectedGatewayAuthenticated)
                    failedAttempts = 0;
                else
                    failedAttempts++;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                Log.Error(e, "Error in gateway connection keeper background service");
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            if (failedAttempts > 0)
                await Task.Delay(ReconnectInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Connects to a gateway server and manages the connection lifecycle.
    /// </summary>
    /// <param name="gatewayServer">The gateway server URL</param>
    /// <param name="stoppingToken">Cancellation token to stop the loop</param>
    /// <returns>A task that represents the connection state</returns>
    private async Task<ConnectionState> ConnectToGateway(string gatewayServer, CancellationToken stoppingToken)
    {
        using var ws = new ClientWebSocket();
        var state = new SocketState
        {
            WebSocket = ws,
            ConnectionState = ConnectionState.ConnectedGatewayUnauthenticated,
            ConnectedOn = DateTimeOffset.Now,
            RegisteredAgentId = null
        };

        using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        try
        {
            await ws.ConnectAsync(new Uri(gatewayServer), stoppingToken);
            gatewayConnectionList.Add(state);

            var timerTask = Task.Run(async () =>
            {
                try
                {
                    while (!stopCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(PingInterval, stopCts.Token);
                        var expirationTime = DateTimeOffset.Now - PingInterval * 2;
                        var isAlive = state.LastReceived > expirationTime;
                        if (state.Authenticated && !isAlive)
                        {
                            await state.WriteMessage(new EnvelopedMessage
                            {
                                Type = MessageTypes.Ping.ToString().ToLowerInvariant(),
                                From = config.InstanceId,
                                MessageId = Guid.NewGuid().ToString()
                            }, WebSockets.Common.WrappingType.PlainText);
                        }
                    }
                }
                catch (OperationCanceledException) when (stopCts.Token.IsCancellationRequested)
                {
                }
            }, stoppingToken);

            Log.Information("Connected to gateway {GatewayServer}", gatewayServer);
            using var scope = serviceScopeFactory.CreateScope();
            var messageLoop = ActivatorUtilities.CreateInstance<WebSocketMessageLoop>(
                scope.ServiceProvider,
                state,
                MessageHandlers.ToMessageHandlerFactory(scope.ServiceProvider));

            await Task.WhenAny(timerTask, messageLoop.RunMessageLoop(stopCts.Token));
        }
        catch (Exception e)
        {
            if (state.Authenticated)
                Log.Warning(e, "Disconnected from gateway {GatewayServer}, will retry", gatewayServer);
            else
                Log.Error(e, "Error connecting to gateway {GatewayServer}", gatewayServer);
        }
        finally
        {
            stopCts.Cancel();
            gatewayConnectionList.Remove(state);
            try
            {
                if (state.WebSocket is { State: WebSocketState.Open or WebSocketState.CloseReceived })
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await state.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cts.Token);
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error while closing the websocket to gateway {GatewayServer}", gatewayServer);
            }
            state.WebSocket?.Dispose();
            Log.Information("Disconnected from gateway {GatewayServer}", gatewayServer);
        }

        return state.ConnectionState;
    }

    /// <summary>
    /// List of message handlers for the gateway client
    /// </summary>
    private static readonly Dictionary<string, Type> MessageHandlers = new()
    {
        { AuthGatewayBehavior.Command, typeof(AuthGatewayBehavior) },
        { WelcomeBehavior.Command, typeof(WelcomeBehavior) },
        { PongBehavior.Command, typeof(PongBehavior) },
        { ProxyBehavior.Command, typeof(ProxyBehavior) },
    };
}