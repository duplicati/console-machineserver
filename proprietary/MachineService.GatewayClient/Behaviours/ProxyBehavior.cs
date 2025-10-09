
// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using MachineService.Common;
using MachineService.Common.Exceptions;
using MachineService.Common.Interfaces;
using MachineService.Common.Model;
using MachineService.Common.Services;
using Serilog;
using WebSockets.Common;
using WebSockets.Common.Model;

namespace MachineService.GatewayClient.Behaviours;

/// <summary>
/// Behavior to handle proxying messages between clients via the gateway
/// </summary>
/// <param name="connectionListService">The connection list service</param>
public class ProxyBehavior(ConnectionListService connectionListService) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.Proxy.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        if (!state.Authenticated || state.ConnectionState != ConnectionState.ConnectedGatewayAuthenticated)
            throw new PolicyViolationException(ErrorMessages.InvalidConnectionStateForAuthentication);

        var proxyMessage = message.DeserializePayload<ProxyMessage>()
            ?? throw new PolicyViolationException(ErrorMessages.InvalidProxyPayload);

        if (!Enum.TryParse<MessageTypes>(proxyMessage.Type, true, out var messageType))
            throw new PolicyViolationException(ErrorMessages.InvalidProxyPayload);

        var target = connectionListService.FirstOrDefault(x => x.ClientId == proxyMessage.To && x.Type == ConnectionType.Agent);
        if (target is null)
        {
            Log.Information("Proxy: Client {ClientId} not found", proxyMessage.To);
            var failureResponse = new EnvelopedMessage
            {
                Type = message.Type,
                From = state.ClientId,
                MessageId = message.MessageId,
                To = message.From,
                ErrorMessage = ErrorMessages.DestinationNotAvailableForRelay
            };

            await state.WriteMessage(failureResponse, WrappingType.PlainText, state.ClientPublicKey);
            return;
        }

        if (target.OrganizationId != proxyMessage.OrganizationId)
        {
            Log.Warning("Proxy: Client {ClientId} cross-organization attempt", proxyMessage.To);
            var failureResponse = new EnvelopedMessage
            {
                Type = message.Type,
                From = state.ClientId,
                MessageId = message.MessageId,
                To = message.From,
                ErrorMessage = ErrorMessages.DestinationNotAvailableForRelay
            };

            await state.WriteMessage(failureResponse, WrappingType.PlainText, state.ClientPublicKey);
            return;
        }

        Log.Debug("Forwarding proxied {MessageType} message from {From}@{OrganizationId} to {To}", proxyMessage.Type, proxyMessage.From, proxyMessage.OrganizationId, proxyMessage.To);
        state.MarkAsInterestedIn(proxyMessage.OrganizationId, proxyMessage.To);
        await target.WriteMessage(new EnvelopedMessage
        {
            Type = proxyMessage.Type,
            From = proxyMessage.From,
            To = proxyMessage.To,
            MessageId = message.MessageId,
            Payload = proxyMessage.InnerMessage
        }, WrappingType.Encrypt, target.ClientPublicKey);

    }
}
