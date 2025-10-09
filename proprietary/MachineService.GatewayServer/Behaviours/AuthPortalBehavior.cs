// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using MachineService.Common;
using MachineService.Common.Enums;
using MachineService.Common.Exceptions;
using MachineService.Common.Interfaces;
using MachineService.Common.Model;
using MachineService.Common.Services;
using MachineService.State.Interfaces;
using Serilog;
using WebSockets.Common;
using WebSockets.Common.Model;

namespace MachineService.GatewayServer.Behaviours;

/// <summary>
/// Behavior for handling authentication messages from Portal clients
/// </summary>
/// <param name="envConfig">The environment configuration</param>
/// <param name="derivedConfig">The derived configuration</param>
/// <param name="backendRelayConnection">The backend relay connection service</param>
/// <param name="statisticsGatherer">The statistics gatherer service</param>
/// <param name="stateManagerService">The state manager service</param>
public class AuthPortalBehavior(
    EnvironmentConfig envConfig,
    DerivedConfig derivedConfig,
    IBackendRelayConnection backendRelayConnection,
    IStatisticsGatherer statisticsGatherer,
    IStateManagerService stateManagerService) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.AuthPortal.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        Log.Debug("Authenticating portal {ClientId}", message.From);

        if (string.IsNullOrWhiteSpace(message.Payload))
            throw new PolicyViolationException(ErrorMessages.AuthMessageWithoutPayload);

        // We only accept authportal from clients originally identified as Portal
        // via the route on the connection, who would be in the following states
        // The reason we allow Authenticated to call it again, is to allow easy an
        // way to keep a connection by simply authenticating again in the same socket.
        switch (state.ConnectionState)
        {
            case ConnectionState.ConnectedPortalUnauthenticated:
            case ConnectionState.ConnectedPortalAuthenticated:
                break;
            default:
                throw new PolicyViolationException(ErrorMessages.InvalidConnectionStateForAuthentication);
        }

        AuthMessage authRequest;
        try
        {
            authRequest = message.DeserializePayload<AuthMessage>() ?? throw new InvalidOperationException();
        }
        catch (Exception)
        {
            throw new PolicyViolationException(ErrorMessages.InvalidAuthPayload);
        }

        if (string.IsNullOrWhiteSpace(authRequest?.Token))
            throw new PolicyViolationException(ErrorMessages.AuthMessageWithoutToken);

        var authResult = await backendRelayConnection.ValidateOAuthToken(authRequest.Token);
        if (authResult.Success)
        {
            state.OrganizationId = authResult.OrganizationId;
            state.TokenExpiration = authResult.TokenExpiration;
            state.ClientId = message.From!;
            state.ClientVersion = authRequest.ClientVersion;
            state.ConnectionState = ConnectionState.ConnectedPortalAuthenticated;
            await stateManagerService.RegisterClient(state.Type, state.ConnectionId, state.ClientId, state.OrganizationId!,
                state.RegisteredAgentId, state.ClientVersion, envConfig.InstanceId, state.RemoteIpAddress);
        }

        var response = new EnvelopedMessage
        {
            Type = MessageTypes.AuthPortal.ToString().ToLowerInvariant(),
            From = envConfig.InstanceId,
            MessageId = Guid.NewGuid().ToString(),
            To = message.From,
            Payload = EnvelopedMessage.SerializePayload(new AuthResultMessage(
                    Accepted: authResult.Success,
                    WillReplaceToken: false,
                    null
            ))
        };

        Log.Debug("Authenticating portal {ClientId}. Result: {result} OrganizationID: {organizationID}", message.From, authResult.Success ? "Success" : "Failed", authResult.OrganizationId);

        // For authportal, it can only be sent in plaintext.
        await state.WriteMessage(response, WrappingType.PlainText, derivedConfig.PrivateKey);

        if (authResult.Success)
            Log.Debug("Authenticating portal {From} - Success.", message.From);
        else
            Log.Information("Authenticating portal {From} - Failure {Reason}.", message.From, authResult.Exception);

        statisticsGatherer.Increment(authResult.Success
            ? StatisticsType.AuthPortalSuccess
            : StatisticsType.AuthPortalFailure);
    }
}