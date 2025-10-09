// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using MachineService.Common;
using MachineService.Common.Enums;
using MachineService.Common.Exceptions;
using MachineService.Common.Interfaces;
using MachineService.Common.Model;
using MachineService.Common.Services;
using Serilog;
using WebSockets.Common;
using WebSockets.Common.Model;

namespace MachineService.GatewayServer.Behaviours;

/// <summary>
/// Handles response to proxy messages from agent to console
/// </summary>
/// <param name="statisticsGatherer">The service for gathering statistics</param>
/// <param name="connectionListService">The service for managing active connections</param>
/// <param name="pendingAgentControlService">The service for managing pending agent control messages</param>
/// <param name="listBehavior">The behavior for handling list messages</param>
public class ProxyBehavior(
    IStatisticsGatherer statisticsGatherer,
    ConnectionListService connectionListService,
    IPendingAgentControlService pendingAgentControlService,
    ListBehavior listBehavior) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.Proxy.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        if (state is { Authenticated: true, ClientId: not null, Type: ConnectionType.Gateway })
        {
            Log.Debug("Got proxy response message from {From}@{OrganizationId} to {To}", message.From, state.OrganizationId, message.To);

            var proxyMessage = message.DeserializePayload<ProxyMessage>()
                ?? throw new PolicyViolationException(ErrorMessages.InvalidProxyPayload);

            if (!string.IsNullOrWhiteSpace(message.ErrorMessage))
            {
                Log.Information("Proxy message contains error message: {ErrorMessage}", message.ErrorMessage);
                return;
            }

            if (!Enum.TryParse<MessageTypes>(proxyMessage.Type, true, out var messageType))
                throw new PolicyViolationException(ErrorMessages.InvalidProxyPayload);

            switch (messageType)
            {
                case MessageTypes.Command:
                    await ProcessCommandMessage(message, proxyMessage);
                    break;
                case MessageTypes.Control:
                    await ProcessControlMessage(message, proxyMessage);
                    break;
                case MessageTypes.List:
                    await ProcessListMessage(message, proxyMessage);
                    break;
                default:
                    Log.Warning("Unsupported proxied message type {MessageType}", proxyMessage.Type);
                    statisticsGatherer.Increment(StatisticsType.InvalidProxyCommand);
                    break;
            }
        }
        else
        {
            Log.Warning($"Non authenticated agent, will not forward.");
            statisticsGatherer.Increment(StatisticsType.ControlRelayDestinationNotAuthenticated);
        }
    }

    /// <summary>
    /// Handles the response to a proxied command message
    /// </summary>
    /// <param name="sourceMessage">The original enveloped message</param>
    /// <param name="proxyMessage">The inner proxy message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task ProcessCommandMessage(EnvelopedMessage sourceMessage, ProxyMessage proxyMessage)
    {
        var target = connectionListService.FirstOrDefault(x => x.ClientId == proxyMessage.To);
        if (target is null)
        {
            Log.Information("No target found for proxy message response to {To}", proxyMessage.To);
            statisticsGatherer.Increment(StatisticsType.ControlRelayDestinationNotAvailable);
            return;
        }

        if (target.OrganizationId != proxyMessage.OrganizationId)
        {
            Log.Warning("Target organization does not match proxy message organization, from {From},{FromOrganizationId} to {To},{ToOrganizationId}", proxyMessage.From, proxyMessage.OrganizationId, proxyMessage.To, target.OrganizationId);
            statisticsGatherer.Increment(StatisticsType.CommandRelayDestinationNotAvailable);
            return;
        }

        var message = new EnvelopedMessage
        {
            Type = proxyMessage.Type,
            From = proxyMessage.From,
            To = proxyMessage.To,
            MessageId = sourceMessage.MessageId,
            Payload = proxyMessage.InnerMessage
        };

        await target.WriteMessage(message, WrappingType.PlainText, target.ClientPublicKey);
    }

    /// <summary>
    /// Handles the response to a proxied control message
    /// </summary>
    /// <param name="sourceMessage">The original enveloped message</param>
    /// <param name="proxyMessage">The inner proxy message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private Task ProcessControlMessage(EnvelopedMessage sourceMessage, ProxyMessage proxyMessage)
    {
        var controlResponse = proxyMessage.DeserializeInnerMessage<ControlResponseMessage>()
            ?? throw new PolicyViolationException(ErrorMessages.InvalidControlResponsePayload);

        pendingAgentControlService.SetControlResponse(proxyMessage.OrganizationId, proxyMessage.From, sourceMessage.MessageId ?? "", controlResponse);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the response to a proxied list message
    /// </summary>
    /// <param name="sourceMessage">The original enveloped message</param>
    /// <param name="proxyMessage">The inner proxy message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task ProcessListMessage(EnvelopedMessage sourceMessage, ProxyMessage proxyMessage)
    {
        var targets = connectionListService.GetConnections().Where(x =>
            x.OrganizationId == proxyMessage.OrganizationId &&
            x.ConnectionState == ConnectionState.ConnectedPortalAuthenticated);

        foreach (var target in targets)
        {
            try
            {
                await listBehavior.ExecuteAsync(target, new EnvelopedMessage
                {
                    From = proxyMessage.From,
                    To = target.ClientId,
                    Type = MessageTypes.List.ToString().ToLowerInvariant(),
                    MessageId = sourceMessage.MessageId
                });
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to forward list response to portal {PortalId}", target.ClientId);
            }
        }
    }
}