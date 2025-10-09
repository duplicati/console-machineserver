// Copyright (c) 2025 Duplicati Inc. All rights reserved.

using MachineService.Common.Interfaces;
using MachineService.Common.Model;
using MachineService.State.Interfaces;
using Serilog;

namespace MachineService.Server.Events;

/// <summary>
/// This class is responsible for handling the behavior of the server after a client has been authenticated.
/// </summary>
/// <param name="stateManagerService">DI injected to manage the state of connected clients</param>
public class GatewayAfterDisconnectBehavior(IStateManagerService stateManagerService) : IAfterDisconnectBehavior
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
                if (state.ConnectionState is ConnectionState.ConnectedAgentAuthenticated or ConnectionState.ConnectedPortalAuthenticated or ConnectionState.ConnectedGatewayAuthenticated)
                {
                    await stateManagerService.DeRegisterClient(state.ConnectionId, state.ClientId ?? "", state.OrganizationId ?? "", state.BytesReceived, state.BytesSent);
                    Log.Debug($"Client disconnected {state.ClientId} and removed from state manager.");
                }
            }
            catch (Exception e)
            {
                Log.Error("Failed to register agent disconnected on state manager, investigate, non critical, agent registration will expire on database view", e);
            }
        }
        catch (Exception e)
        {
            Log.Error("Failure during AfterDisconnectBehavior", e);
        }
    }
}