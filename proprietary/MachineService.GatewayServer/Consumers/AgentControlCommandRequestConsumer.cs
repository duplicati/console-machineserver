// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using MachineService.Common.Enums;
using MachineService.Common.Interfaces;
using MachineService.Common.Model;
using MachineService.Common.Services;
using MachineService.External;
using MachineService.State.Interfaces;
using MassTransit;
using WebSockets.Common;
using WebSockets.Common.Model;

namespace MachineService.GatewayServer.Consumers;

/// <summary>
/// Consumer for handling agent control requests from the console
/// </summary>
/// <param name="stateManagerService">The state manager service</param>
/// <param name="statisticsGatherer">The statistics gatherer service</param>
/// <param name="envConfig">The environment configuration</param>
/// <param name="connectionList">The connection list service</param>
/// <param name="pendingAgentControlService">The pending agent control service</param>
public class AgentControlCommandRequestHandler(
    IStateManagerService stateManagerService,
    IStatisticsGatherer statisticsGatherer,
    EnvironmentConfig envConfig,
    ConnectionListService connectionList,
    IPendingAgentControlService pendingAgentControlService) : IConsumer<AgentControlCommandRequest>
{
    /// <summary>
    /// The time to wait for a control response from the agent
    /// </summary>
    private static readonly TimeSpan ControlResponseTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AgentControlCommandRequest> context)
    {
        var targetClient = (await stateManagerService.GetAgents(context.Message.OrganizationId))
            .FirstOrDefault(x => x.Type == ConnectionType.Agent && x.MachineRegistrationId == context.Message.AgentId);

        var destination = connectionList.FirstOrDefault(x => x.ClientId == targetClient?.GatewayId && x.Type == ConnectionType.Gateway);
        if (destination is null || targetClient is null || targetClient.OrganizationId != context.Message.OrganizationId)
        {
            statisticsGatherer.Increment(StatisticsType.ControlRelayDestinationNotAvailable);
            await context.RespondAsync(new AgentControlCommandResponse(
                AgentId: context.Message.AgentId,
                OrganizationId: context.Message.OrganizationId,
                Settings: null,
                Success: false,
                Message: "Client was not connected"
            ));

            return;
        }

        statisticsGatherer.Increment(StatisticsType.ControlRelayInitiated);
        try
        {
            // Prepare the message
            var message = new EnvelopedMessage()
            {
                From = envConfig.InstanceId,
                To = destination.ClientId,
                Type = MessageTypes.Proxy.ToString().ToLowerInvariant(),
                MessageId = Guid.NewGuid().ToString(),
                Payload = EnvelopedMessage.SerializePayload(new ProxyMessage
                {
                    Type = MessageTypes.Control.ToString().ToLowerInvariant(),
                    From = envConfig.InstanceId!,
                    To = targetClient.ClientId,
                    OrganizationId = targetClient.OrganizationId,
                    InnerMessage = EnvelopedMessage.SerializePayload(new ControlRequestMessage(
                        Command: context.Message.Command,
                        Parameters: context.Message.Settings
                    ))
                })
            };

            // Prepare to wait for a response
            using var ct = new CancellationTokenSource(ControlResponseTimeout);
            var responseTask = pendingAgentControlService.PrepareForControlResponse(targetClient.OrganizationId, targetClient.ClientId, message.MessageId, ct.Token);

            // Send the message
            await destination.WriteMessage(message, WrappingType.PlainText, destination.ClientPublicKey);

            // Wait for the response
            var response = await responseTask;

            statisticsGatherer.Increment(StatisticsType.ControlRelaySuccess);

            // Relay the response back to the backend
            await context.RespondAsync(new AgentControlCommandResponse(
                AgentId: context.Message.AgentId,
                OrganizationId: context.Message.OrganizationId,
                Settings: response.Output,
                Success: response.Success,
                Message: response.ErrorMessage
            ));
        }
        catch (Exception ex)
        {
            statisticsGatherer.Increment(StatisticsType.ControlRelayFailure);
            await context.RespondAsync(new AgentControlCommandResponse(
                AgentId: context.Message.AgentId,
                OrganizationId: context.Message.OrganizationId,
                Settings: null,
                Success: false,
                Message: $"Failed to send message to client: {ex.Message}"
            ));
        }
    }
}
