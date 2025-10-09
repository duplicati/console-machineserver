// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using MachineService.Common;
using MachineService.Common.Enums;
using MachineService.Common.Interfaces;
using MachineService.Common.Model;
using MachineService.Common.Services;
using MachineService.State.Interfaces;
using Serilog;
using WebSockets.Common;
using WebSockets.Common.Model;

namespace MachineService.GatewayServer.Behaviours;

/// <summary>
/// Behavior for handling command messages
/// </summary>
/// <param name="envConfig">The environment configuration</param>
/// <param name="derivedConfig">The derived configuration</param>
/// <param name="connectionList">The connection list service</param>
/// <param name="statisticsGatherer">The statistics gatherer service</param>
/// <param name="stateManagerService">The state manager service</param>
public class CommandBehavior(
    EnvironmentConfig envConfig,
    DerivedConfig derivedConfig,
    ConnectionListService connectionList,
    IStatisticsGatherer statisticsGatherer,
    IStateManagerService stateManagerService) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.Command.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        if (state is { Authenticated: true, OrganizationId: not null, ConnectionState: ConnectionState.ConnectedPortalAuthenticated })
        {
            Log.Debug("Relaying command message from {From}@{OrganizationId} to {To}", message.From, state.OrganizationId, message.To);
            var targetClient = (await stateManagerService.GetAgents(state.OrganizationId))
                .FirstOrDefault(x => x.Type == ConnectionType.Agent && x.ClientId == message.To);

            var destination = connectionList.FirstOrDefault(x => x.ClientId == targetClient?.GatewayId && x.Type == ConnectionType.Gateway);
            if (destination is not null && targetClient is not null && message.MessageId is not null && targetClient.OrganizationId == state.OrganizationId)
            {
                // Proxy destination exists, forward the message with an extra envelope
                var proxyMessage = new EnvelopedMessage
                {
                    Type = MessageTypes.Proxy.ToString().ToLowerInvariant(),
                    From = envConfig.InstanceId,
                    To = destination.ClientId,
                    MessageId = message.MessageId,
                    Payload = EnvelopedMessage.SerializePayload(new ProxyMessage
                    {
                        Type = message.Type ?? throw new InvalidOperationException("Message type is not available, and should be"),
                        From = state.ClientId ?? throw new InvalidOperationException("ClientId is not available, and should be"),
                        To = targetClient.ClientId,
                        OrganizationId = state.OrganizationId!,
                        InnerMessage = message.Payload!
                    })
                };

                await destination.WriteMessage(proxyMessage, WrappingType.PlainText);
                statisticsGatherer.Increment(StatisticsType.CommandRelaySuccess);
            }
            else
            {
                var response = new EnvelopedMessage
                {
                    Type = message.Type,
                    From = envConfig.InstanceId,
                    MessageId = message.MessageId,
                    To = message.From,
                    ErrorMessage = ErrorMessages.DestinationNotAvailableForRelay
                };

                await state.WriteMessage(response, derivedConfig);
                statisticsGatherer.Increment(StatisticsType.CommandRelayDestinationNotAvailable);
            }
        }
        else
        {
            Log.Warning("Not authenticated portal, will not relay, connection state: {ConnectionState}, organization ID: {OrganizationId}.", state.ConnectionState, state.OrganizationId);
        }
    }
}