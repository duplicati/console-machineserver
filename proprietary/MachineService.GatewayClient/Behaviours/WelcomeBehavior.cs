
// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using MachineService.Common;
using MachineService.Common.Exceptions;
using MachineService.Common.Interfaces;
using MachineService.Common.Model;
using MachineService.Common.Util;
using Serilog;
using WebSockets.Common;
using WebSockets.Common.Model;

namespace MachineService.GatewayClient.Behaviours;

/// <summary>
/// Behavior to handle the welcome message from the gateway
/// </summary>
/// <param name="envConfig">The environment configuration</param>
/// <param name="derivedConfig">The derived configuration</param>
public class WelcomeBehavior(EnvironmentConfig envConfig, DerivedConfig derivedConfig) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.Welcome.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        // Only allow the welcome message when we have not yet authenticated or seen a welcome message
        if (state.Authenticated || state.ConnectionState != ConnectionState.ConnectedGatewayUnauthenticated || state.GatewayAuthHash != null || state.NonceBytes != null)
            throw new PolicyViolationException(ErrorMessages.InvalidConnectionStateForAuthentication);

        var welcome = message.DeserializePayload<WelcomeMessage>()
            ?? throw new PolicyViolationException(ErrorMessages.InvalidAuthPayload);

        if (string.IsNullOrWhiteSpace(welcome.Nonce))
            throw new PolicyViolationException(ErrorMessages.IncorrectGatewayHandshake);

        state.NonceBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        state.GatewayAuthHash = AuthHandshake.CreateThreePartHash(envConfig.GatewayPreSharedKey!, Convert.FromBase64String(welcome.Nonce!), state.NonceBytes);
        var response = new EnvelopedMessage
        {
            Type = MessageTypes.AuthGateway.ToString().ToLowerInvariant(),
            From = envConfig.InstanceId,
            MessageId = Guid.NewGuid().ToString(),
            To = message.From,
            Payload = EnvelopedMessage.SerializePayload(new AuthGatewayMessage(
                Convert.ToBase64String(state.NonceBytes),
                state.GatewayAuthHash
            ))
        };

        // For authgateway, it can only be sent in plaintext.
        await state.WriteMessage(response, WrappingType.PlainText, derivedConfig.PrivateKey);

        Log.Debug("Authenticated gateway {From}, {ClientId} - Success.", message.From, state.ClientId);
    }
}
