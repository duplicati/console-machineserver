// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using MachineService.Common.Enums;
using MachineService.Common.Interfaces;
using MachineService.Common.Model;
using MachineService.Common.Services;
using MachineService.State.Interfaces;
using Serilog;
using WebSockets.Common.Model;

namespace MachineService.GatewayServer.Behaviours;

/// <summary>
/// Behavior for handling ping messages
/// </summary>
/// <param name="settings">The environment configuration</param>
/// <param name="derived">The derived configuration</param>
/// <param name="statisticsGatherer">The statistics gatherer service</param>
/// <param name="stateManagerService">The state manager service</param>
public class PingBehavior(
    EnvironmentConfig settings,
    DerivedConfig derived,
    IStatisticsGatherer statisticsGatherer,
    IStateManagerService stateManagerService) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.Ping.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        Log.Debug("Ping request from {From}", message.From);

        // If authenticated and connected, refresh the client registration
        if (state is { Authenticated: true, ConnectionState: ConnectionState.ConnectedPortalAuthenticated or ConnectionState.ConnectedGatewayAuthenticated })
        {
            await stateManagerService.UpdateClientActivity(state.ClientId ?? "", state.OrganizationId ?? "", CancellationToken.None);

            statisticsGatherer.Increment(StatisticsType.PingCommandSuccess);
            await state.WriteMessage(new EnvelopedMessage
            {
                Type = MessageTypes.Pong.ToString().ToLowerInvariant(),
                From = settings.InstanceId,
                MessageId = Guid.NewGuid().ToString(),
                To = message.From
            }, derived);
        }
        else
            Log.Debug("Ignoring ping request from {ClientId} in state {ConnectionState}", state.ClientId, state.ConnectionState);
    }
}