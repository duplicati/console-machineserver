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
using Microsoft.Extensions.Hosting;
using Serilog;

namespace MachineService.Common.Services;

/// <summary>
/// Manages WebSocket connections lifecycle and ensures graceful shutdown
/// </summary>
public class WebSocketConnectionManager : IHostedService
{
    /// <summary>
    /// The connection list service to track active connections
    /// </summary>
    private readonly ConnectionListService _connectionListService;
    /// <summary>
    /// The gateway connection list to track active gateway connections
    /// </summary>
    private readonly GatewayConnectionList _gatewayConnectionList;
    /// <summary>
    /// The application lifetime to hook into shutdown events
    /// </summary>
    private readonly IHostApplicationLifetime _lifetime;
    /// <summary>
    /// Cancellation token source to signal shutdown to message loops
    /// </summary>
    private CancellationTokenSource? _shutdownCts;

    /// <summary>
    /// Initializes a new instance of the WebSocketConnectionManager
    /// </summary>
    /// <param name="connectionListService">The connection list service</param>
    /// <param name="gatewayConnectionList">The gateway connection list</param>
    /// <param name="lifetime">The application lifetime</param>
    public WebSocketConnectionManager(
        ConnectionListService connectionListService,
        GatewayConnectionList gatewayConnectionList,
        IHostApplicationLifetime lifetime)
    {
        _connectionListService = connectionListService;
        _gatewayConnectionList = gatewayConnectionList;
        _lifetime = lifetime;
    }

    /// <summary>
    /// Gets the cancellation token that signals when the application is shutting down
    /// </summary>
    public CancellationToken ShutdownToken => _shutdownCts?.Token ?? CancellationToken.None;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _shutdownCts = new CancellationTokenSource();

        // Register for shutdown notification
        _lifetime.ApplicationStopping.Register(OnShutdown);

        Log.Debug("WebSocket Connection Manager started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("WebSocket Connection Manager stopping - closing all connections");

        // Signal shutdown to all message loops
        _shutdownCts?.Cancel();

        // Close all active WebSocket connections
        await CloseAllConnections(cancellationToken);

        Log.Information("All WebSocket connections closed");
    }

    /// <summary>
    /// Called when the application is shutting down
    /// </summary>
    private void OnShutdown()
    {
        Log.Information("Application shutdown initiated - preparing to close WebSocket connections");
    }

    /// <summary>
    /// Closes all active WebSocket connections gracefully
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task CloseAllConnections(CancellationToken cancellationToken)
    {
        var connections = _connectionListService.GetConnections().ToList();
        Log.Information("Closing {Count} active WebSocket connections", connections.Count);

        var closeConnectionTasks = connections.Select(async connection =>
        {
            try
            {
                if (connection.WebSocket.State == WebSocketState.Open)
                {
                    await connection.WebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server shutting down",
                        cancellationToken);

                    Log.Debug("Closed connection for client {ClientId}", connection.ClientId);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error closing connection for client {ClientId}", connection.ClientId);
            }
        });

        var gatewayConnections = _gatewayConnectionList.Where(_ => true).ToList();
        Log.Information("Closing {Count} active Gateway WebSocket connections", gatewayConnections.Count);

        var closeGatewayTasks = gatewayConnections.Select(async connection =>
        {
            try
            {
                if (connection.WebSocket.State == WebSocketState.Open)
                {
                    await connection.WebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server shutting down",
                        cancellationToken);

                    Log.Debug("Closed gateway connection for client {ClientId}", connection.ClientId);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error closing gateway connection for client {ClientId}", connection.ClientId);
            }
        });

        // Wait for all connections to close with timeout
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        var allClosedTask = Task.WhenAll(closeConnectionTasks.Concat(closeGatewayTasks));

        await Task.WhenAny(allClosedTask, timeoutTask);

        if (!allClosedTask.IsCompleted)
        {
            Log.Warning("Timeout waiting for all connections to close gracefully");
        }
    }
}