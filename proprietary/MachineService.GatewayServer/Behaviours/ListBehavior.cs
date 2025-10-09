// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using MachineService.Common;
using MachineService.Common.Enums;
using MachineService.Common.Exceptions;
using MachineService.Common.Interfaces;
using MachineService.Common.Model;
using MachineService.Common.Services;
using MachineService.State.Interfaces;
using Serilog;
using WebSockets.Common.Model;

namespace MachineService.GatewayServer.Behaviours;

/// <summary>
/// Behavior for handling list messages
/// </summary>
/// <param name="envConfig">The environment configuration</param>
/// <param name="derivedConfig">The derived configuration</param>
/// <param name="statisticsGatherer">The statistics gatherer service</param>
/// <param name="stateManagerService">The state manager service</param>
public class ListBehavior(
    EnvironmentConfig envConfig,
    DerivedConfig derivedConfig,
    IStatisticsGatherer statisticsGatherer,
    IStateManagerService stateManagerService) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.List.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        Log.Debug("List request from {From}", message.From);

        if (state is { Authenticated: true, OrganizationId: not null })
        {
            if (state.ConnectionState != ConnectionState.ConnectedPortalAuthenticated)
                throw new PolicyViolationException(ErrorMessages.InvalidConnectionStateForList);

            var agentsForOrganization = await stateManagerService.GetAgents(state.OrganizationId);
            Log.Debug("Returning {Count} clients for organization {OrganizationId}", agentsForOrganization.Count, state.OrganizationId);

            await state.WriteMessage(new EnvelopedMessage
            {
                Type = message.Type,
                From = envConfig.InstanceId,
                MessageId = message.MessageId,
                To = state.ClientId,
                Payload = EnvelopedMessage.SerializePayload(agentsForOrganization)
            }, derivedConfig);

            statisticsGatherer.Increment(StatisticsType.ListCommandSuccess);
        }
        else
        {
            Log.Warning($"Non authenticated, will not reply.");
        }
    }
}