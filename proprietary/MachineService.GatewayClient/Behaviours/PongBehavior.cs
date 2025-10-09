
// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using MachineService.Common;
using MachineService.Common.Exceptions;
using MachineService.Common.Interfaces;
using MachineService.Common.Model;
using Serilog;
using WebSockets.Common.Model;

namespace MachineService.GatewayClient.Behaviours;

/// <summary>
/// Behavior to handle pong messages from the gateway
/// </summary>
public class PongBehavior : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.Pong.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        if (!state.Authenticated || state.ConnectionState != ConnectionState.ConnectedGatewayAuthenticated)
            throw new PolicyViolationException(ErrorMessages.InvalidConnectionStateForAuthentication);

        Log.Debug("Pong on gateway {From}, {ClientId} - Success.", message.From, state.ClientId);
        return Task.CompletedTask;
    }
}
