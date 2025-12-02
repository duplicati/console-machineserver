// Copyright (c) 2025 Duplicati Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using MachineService.Common;
using MachineService.Common.Enums;
using MachineService.Common.Exceptions;
using MachineService.Common.Services;
using MachineService.State.Interfaces;

namespace MachineService.Server.Behaviours;

/// <summary>
/// Behavior for handling authentication messages from Portal clients. Does not include post-authentication hooks as portals do not require activity tracking.
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
    public static string Command => MessageTypes.AuthPortal.ToString().ToLowerInvariant();

    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        Log.Debug("Authenticating portal {ClientId}", message.From);

        if (string.IsNullOrWhiteSpace(message.Payload))
            throw new PolicyViolationException(ErrorMessages.AuthMessageWithoutPayload);

        // We only accept authportal from clients originally identified as Portal
        // via the route on the connection, who would be in the following states
        // The reason we allow Authenticated to call it again, is to allow an easy
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
            state.Impersonated = authResult.Impersonated;
            await stateManagerService.RegisterClient(state.Type, state.ConnectionId, state.ClientId, state.OrganizationId!,
                state.RegisteredAgentId, state.ClientVersion, envConfig.InstanceId, state.RemoteIpAddress, CancellationToken.None);
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