// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using MachineService.Common;
using MachineService.Common.Exceptions;
using MachineService.Common.Interfaces;
using MachineService.Common.Model;
using MachineService.Common.Util;
using Serilog;
using WebSockets.Common.Model;

namespace MachineService.GatewayClient.Behaviours;

/// <summary>
/// Behavior to authenticate a gateway connection
/// </summary>
/// <param name="envConfig">The environment configuration</param>
public class AuthGatewayBehavior(EnvironmentConfig envConfig) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.AuthGateway.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        // Only allow the auth message when we have seen the welcome message and not yet authenticated
        if (state.Authenticated || state.ConnectionState != ConnectionState.ConnectedGatewayUnauthenticated || state.GatewayAuthHash == null || state.NonceBytes == null)
            throw new PolicyViolationException(ErrorMessages.InvalidConnectionStateForAuthentication);

        var auth = message.DeserializePayload<AuthGatewayMessage>()
            ?? throw new PolicyViolationException(ErrorMessages.InvalidAuthPayload);

        var authRequestHash = AuthHandshake.CreateThreePartHash(envConfig.GatewayPreSharedKey!, Convert.FromBase64String(state.GatewayAuthHash), state.NonceBytes);
        if (!string.Equals(auth.Hash, authRequestHash, StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("Failed to authenticate gateway {From}, {ClientId} - Invalid hash.", message.From, state.ClientId);
            throw new PolicyViolationException(ErrorMessages.IncorrectGatewayHandshake);
        }

        state.ConnectionState = ConnectionState.ConnectedGatewayAuthenticated;
        state.ClientId = message.From;

        Log.Debug("Authenticated gateway {From}, {ClientId} - Success.", message.From, state.ClientId);
        return Task.CompletedTask;
    }
}
