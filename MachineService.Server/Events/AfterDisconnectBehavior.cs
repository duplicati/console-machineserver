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
using MachineService.Common.Services;
using MachineService.External;
using MachineService.Server.Utility;
using MachineService.State.Interfaces;

namespace MachineService.Server.Events;

/// <summary>
/// This class is responsible for handling the behavior of the server after a client disconnects.
/// </summary>
/// <param name="envConfig">DI Injected settings</param>
/// <param name="listBehavior">DI Injected behavior to process the virtual list commands</param>
/// <param name="connectionListService">DI injected to access all connections to machineserver</param>
/// <param name="gatewayConnectionList">DI injected to access all gateway connections to machineserver</param>
/// <param name="publishAgentActivityService">DI injected to publish agent activity events</param>
/// <param name="stateManagerService">DI injected to manage the state of connected clients</
public class AfterDisconnectBehavior(
    EnvironmentConfig envConfig,
    ListBehavior listBehavior,
    ConnectionListService connectionListService,
    GatewayConnectionList gatewayConnectionList,
    IPublishAgentActivityService publishAgentActivityService,
    IStateManagerService stateManagerService) : IAfterDisconnectBehavior
{
    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state)
    {
        try
        {
            Log.Debug("Executing AfterDisconnectBehavior for {ClientId}", state.ClientId);

            // First lets try to deregister the client from the state manager
            try
            {
                if (state.ConnectionState is ConnectionState.ConnectedAgentAuthenticated or ConnectionState.ConnectedPortalAuthenticated)
                {
                    await stateManagerService.DeRegisterClient(state.ConnectionId, state.ClientId ?? "", state.OrganizationId ?? "", state.BytesReceived, state.BytesSent);
                    Log.Debug($"Client disconnected {state.ClientId} and removed from state manager.");

                    if (state is { Authenticated: true, ConnectionState: ConnectionState.ConnectedAgentAuthenticated })
                        await publishAgentActivityService.Publish(new AgentActivityMessage(ActivityType.Disconnected,
                            state.ConnectedOn, state.RegisteredAgentId ?? throw new InvalidOperationException("RegisteredAgentId is not available, and should be"), state.OrganizationId, state.ClientVersion, null), CancellationToken.None);

                }
            }
            catch (Exception e)
            {
                Log.Error("Failed to register agent disconnected on state manager, investigate, non critical, agent registration will expire on database view", e);
            }

            if (state.ConnectionState is ConnectionState.ConnectedAgentAuthenticated or ConnectionState.ConnectedPortalAuthenticated && !string.IsNullOrWhiteSpace(state.OrganizationId))
            {
                await ForwardListUpdateMessages.ForwardListUpdateToConnectedClients(connectionListService, listBehavior, envConfig.InstanceId!, state.OrganizationId);
                await ForwardListUpdateMessages.ForwardListUpdateToRelevantGateways(gatewayConnectionList, stateManagerService, envConfig.InstanceId!, state.OrganizationId);
            }
        }
        catch (Exception e)
        {
            Log.Error("Failure during AfterDisconnectBehavior", e);
        }
    }
}