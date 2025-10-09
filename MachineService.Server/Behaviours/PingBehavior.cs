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

using MachineService.Common.Enums;
using MachineService.Common.Services;
using MachineService.External;
using MachineService.State.Interfaces;

namespace MachineService.Server.Behaviours;

/// <summary>
/// Behavior for handling ping messages
/// </summary>
/// <param name="settings">The environment configuration</param>
/// <param name="derived">The derived configuration</param>
/// <param name="statisticsGatherer">The statistics gatherer service</param>
/// <param name="publishAgentActivityService">The publish agent activity service</param>
/// <param name="stateManagerService">The state manager service</param>
public class PingBehavior(
    EnvironmentConfig settings,
    DerivedConfig derived,
    IStatisticsGatherer statisticsGatherer,
    IPublishAgentActivityService publishAgentActivityService,
    IStateManagerService stateManagerService) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.Ping.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        Log.Debug("Ping request from {From}", message.From);

        // If authenticated and connected, refresh the client registration
        if (state is { Authenticated: true, ConnectionState: ConnectionState.ConnectedAgentAuthenticated or ConnectionState.ConnectedPortalAuthenticated })
            await stateManagerService.UpdateClientActivity(state.ClientId ?? "", state.OrganizationId ?? "", CancellationToken.None);

        if (!settings.DisablePingMessages && state is { Authenticated: true, ConnectionState: ConnectionState.ConnectedAgentAuthenticated })
            await publishAgentActivityService.Publish(new AgentActivityMessage(ActivityType.Ping,
                state.ConnectedOn, state.RegisteredAgentId ?? throw new InvalidOperationException("RegisteredAgentId is not available, and should be"), state.OrganizationId, state.ClientVersion, null), CancellationToken.None);

        // Only answer ping request after authentication & handshake are done.
        if (state.ConnectionState is ConnectionState.ConnectedPortalAuthenticated
            or ConnectionState.ConnectedAgentAuthenticated)
        {
            statisticsGatherer.Increment(StatisticsType.PingCommandSuccess);
            await state.WriteMessage(new EnvelopedMessage
            {
                Type = MessageTypes.Pong.ToString().ToLowerInvariant(),
                From = settings.InstanceId,
                MessageId = Guid.NewGuid().ToString(),
                To = message.From
            }, derived);
        }
        else
            Log.Debug("Ignoring ping request from {ClientId} in state {ConnectionState}", state.ClientId, state.ConnectionState);
    }
}