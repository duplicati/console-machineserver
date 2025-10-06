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
using System.Security.Cryptography;
using MachineService.Common;
using MachineService.Common.Enums;
using MachineService.Common.Exceptions;
using MachineService.Common.Services;
using MachineService.Server.Utility;
using MachineService.State.Interfaces;

namespace MachineService.Server.Behaviours;

/// <summary>
/// Behavior for handling authentication messages
/// </summary>
/// <param name="envConfig">The environment configuration</param>
/// <param name="derivedConfig">The derived configuration</param>
/// <param name="backendRelayConnection">The backend relay connection service</param>
/// <param name="onAuthenticatedClientBehavior">Optional behavior to execute after successful authentication</param>
/// <param name="statisticsGatherer">The statistics gatherer service</param>
/// <param name="stateManagerService">The state manager service</param>
public class AuthBehavior(
    EnvironmentConfig envConfig,
    DerivedConfig derivedConfig,
    IBackendRelayConnection backendRelayConnection,
    IAfterAuthenticatedClientBehavior? onAuthenticatedClientBehavior,
    IStatisticsGatherer statisticsGatherer,
    IStateManagerService stateManagerService) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.Auth.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        Log.Debug("Authenticating client {ClientId}", message.From);

        if (string.IsNullOrWhiteSpace(message.Payload))
            throw new PolicyViolationException(ErrorMessages.AuthMessageWithoutPayload);

        // We only accept auth from clients originally identified as Portal
        // via the route on the connection, who would be in the following states
        // The reason we allow Authenticated to call it again, is to allow easy an
        // way to keep a connection by simply authenticating again in the same socket.
        if (!new List<ConnectionState>
            {
                ConnectionState.ConnectedAgentUnauthenticated,
                ConnectionState.ConnectedAgentAuthenticated
            }.Contains(state.ConnectionState))
            throw new PolicyViolationException(ErrorMessages.InvalidConnectionStateForAuthentication);

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

        if (!derivedConfig.AllowedProtocolVersions.Contains(authRequest.ProtocolVersion))
            throw new PolicyViolationException(ErrorMessages.InvalidProtocolVersion);

        var authResult = await backendRelayConnection.ValidateAgentToken(authRequest.Token);
        EnvelopedMessage? response;

        if (authResult.Success)
        {
            Log.Debug("Authenticating client {ClientId} - Success.", message.From);
            var clientKey = RSA.Create();
            clientKey.ImportFromPem(authRequest.PublicKey!);

            state.ProtocolVersion = authRequest.ProtocolVersion;
            state.ClientPublicKey = clientKey;
            state.ClientVersion = authRequest.ClientVersion;
            state.Authenticated = authResult.Success;
            state.OrganizationId = authResult.OrganizationId;
            state.TokenExpiration = authResult.TokenExpiration;
            state.RegisteredAgentId = authResult.RegisteredAgentId;
            state.Type = ConnectionType.Agent;
            state.ClientId = message.From!;
            state.ConnectionState = ConnectionState.ConnectedAgentAuthenticated;

            await stateManagerService.RegisterClient(state.Type, state.ConnectionId, state.ClientId, state.OrganizationId!,
                state.RegisteredAgentId, state.ClientVersion, ServerUrlBuilder.BuildUrl(), state.RemoteIpAddress);

            response = new EnvelopedMessage
            {
                Type = MessageTypes.Auth.ToString().ToLowerInvariant(),
                From = envConfig.MachineName,
                MessageId = message.MessageId,
                To = message.From,
                Payload = EnvelopedMessage.SerializePayload(new AuthResultMessage(
                        Accepted: true,
                        WillReplaceToken: !String.IsNullOrEmpty(authResult.NewToken),
                        authResult.NewToken
                ))
            };

            // Execute the behavior after the client has been authenticated
            if (onAuthenticatedClientBehavior is not null)
                await onAuthenticatedClientBehavior.ExecuteAsync(state, authRequest.Metadata);

            statisticsGatherer.Increment(StatisticsType.AuthClientSuccess);
        }
        else
        {
            if (authResult.Exception?.Message.Contains("Agent not found") == true)
                Log.Information("Authenticating client {From} - Failure {Reason}.", message.From, authResult.Exception.Message);
            else
                Log.Warning("Authenticating client {From} - Failure {Reason}.", message.From, authResult.Exception);

            response = new EnvelopedMessage
            {
                Type = MessageTypes.Auth.ToString().ToLowerInvariant(),
                From = envConfig.MachineName,
                MessageId = message.MessageId,
                To = message.From,
                Payload = EnvelopedMessage.SerializePayload(new AuthResultMessage(
                        Accepted: false,
                        WillReplaceToken: false,
                        null
                ))
            };

            if (authResult.Exception is TimeoutException || authResult.Exception is OperationCanceledException || authResult.Exception is MassTransit.RequestTimeoutException)
                statisticsGatherer.Increment(state.Type == ConnectionType.Agent ? StatisticsType.AuthClientTimeoutFailure : StatisticsType.AuthPortalTimeoutFailure);
            else if (authResult.Exception?.Message.Contains("Agent not found") == true)
                statisticsGatherer.Increment(StatisticsType.AuthClientNotFound);
            else
                statisticsGatherer.Increment(StatisticsType.AuthClientFailure);
        }

        await state.WriteMessage(response, derivedConfig);
    }
}