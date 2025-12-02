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
using MachineService.Common.Services;
using MachineService.Server.Utility;

namespace MachineService.Server.Behaviours;

/// <summary>
/// Behavior for handling command messages
/// </summary>
/// <param name="envConfig">The environment configuration</param>
/// <param name="derivedConfig">The derived configuration</param>
/// <param name="connectionList">The connection list service</param>
/// <param name="statisticsGatherer">The statistics gatherer service</param>
/// <param name="gatewayConnectionList">The list of gateway connections</param>
public class CommandBehavior(EnvironmentConfig envConfig, DerivedConfig derivedConfig, ConnectionListService connectionList, IStatisticsGatherer statisticsGatherer, GatewayConnectionList gatewayConnectionList) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.Command.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        if (state is { Authenticated: true, OrganizationId: not null, ClientId: not null })
        {
            // now we get the list from status filtered by organizationId
            // This is a request command, its coming from portal or gateway
            // and its meant to go to an agent.

            Log.Debug("Relaying command message from {From}@{OrganizationId} to {To}", message.From, state.OrganizationId, message.To);

            if (message.To is not null)
            {
                SocketState? destination = null;
                destination = connectionList.FirstOrDefault(x => x.ClientId == message.To);

                if (state.Impersonated)
                {
                    Log.Error("Cross organization message relay attempt {From}@{OrganizationId} to {To}", message.From, state.OrganizationId, message.To);

                    // Break the connection with the requester
                    await state.WebSocket.TerminateWithPolicyViolation("Access denied");
                    // Just in case, reset destination connection as well and let it re-connect and authenticate
                    if (destination is not null)
                        await destination.WebSocket.TerminateGracefully();
                }

                // Forward to relevant gateways
                var any = await gatewayConnectionList.ForwardToRelevantGateways(envConfig.InstanceId!, message, state.OrganizationId, state.ClientId);

                // Don't log a warning if we found any relevant gateway
                if (any && destination is null)
                {
                    statisticsGatherer.Increment(StatisticsType.CommandRelaySuccess);
                    return;
                }

                // We could have selected by organization id here, but specifically did not so we could
                // detect a cross organization attack attempt at protocol level.
                if (destination is not null && destination.OrganizationId == state.OrganizationId)
                {
                    // Destination is registered, and belongs to the organization, relay

                    if (destination.Type == ConnectionType.Portal)
                    {
                        // This is a messsage from an agent to a portal
                        await destination.WriteMessage(message, WrappingType.PlainText);
                        statisticsGatherer.Increment(StatisticsType.CommandRelaySuccess);
                    }
                    else if (destination.Type == ConnectionType.Agent)
                    {
                        await destination.WriteMessage(message, WrappingType.Encrypt,
                            destination.ClientPublicKey);
                        statisticsGatherer.Increment(StatisticsType.CommandRelaySuccess);
                    }
                }
                else if (destination is not null && destination.OrganizationId != state.OrganizationId)
                {
                    Log.Error("Cross organization message relay attempt {From}@{OrganizationId} to {To}", message.From, state.OrganizationId, message.To);

                    // Break the connection with the requester
                    await state.WebSocket.TerminateWithPolicyViolation("Access denied");
                    // Just in case, reset destination connection as well and let it re-connect and authenticate
                    await destination.WebSocket.TerminateGracefully();
                }
                else
                {
                    // Only forward error if the sender is a portal,
                    // as the agent has no use for the error message.
                    if (state.Type == ConnectionType.Portal)
                    {
                        var response = new EnvelopedMessage
                        {
                            Type = message.Type,
                            From = envConfig.InstanceId,
                            MessageId = message.MessageId,
                            To = message.From,
                            ErrorMessage = ErrorMessages.DestinationNotAvailableForRelay
                        };

                        await state.WriteMessage(response, derivedConfig);
                    }
                    statisticsGatherer.Increment(StatisticsType.CommandRelayDestinationNotAvailable);
                }
            }
        }
        else
        {
            Log.Warning("Not authenticated, will not relay, connection state: {ConnectionState}, organization ID: {OrganizationId}.", state.ConnectionState, state.OrganizationId);
        }
    }
}