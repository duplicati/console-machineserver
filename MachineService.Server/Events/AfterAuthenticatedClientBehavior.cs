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

namespace MachineService.Server.Events;

/// <summary>
/// This class is responsible for handling the behavior of the server after a client has been authenticated.
/// </summary>
/// <param name="envConfig">DI Injected settings</param>
/// <param name="listBehavior">DI Injected behavior to process the virtual list commands</param>
/// <param name="connectionListService">DI injected to access all connections to machineserver</param>
public class AfterAuthenticatedClientBehavior(EnvironmentConfig envConfig, ListBehavior listBehavior, ConnectionListService connectionListService, IPublishAgentActivityService publishAgentActivityService) : IAfterAuthenticatedClientBehavior
{
    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, Dictionary<string, string?>? metadata)
    {
        try
        {
            Log.Debug("Executing AfterAuthenticatedClientBehavior for {ClientId}", state.ClientId);

            if (state is { ConnectionState: ConnectionState.ConnectedAgentAuthenticated })
                await publishAgentActivityService.Publish(new AgentActivityMessage(ActivityType.Connected,
                    state.ConnectedOn, state.RegisteredAgentId ?? throw new InvalidOperationException("RegisteredAgentId is not available"), state.OrganizationId, state.ClientVersion, metadata), CancellationToken.None);

            // Proactively send the list of connected clients to all existing and authenticated
            // portal connections
            var listSnapshot = connectionListService.GetConnections();

            foreach (var portalConnection in listSnapshot.Where(x =>
                         x.OrganizationId == state.OrganizationId &&
                         x.ConnectionState == ConnectionState.ConnectedPortalAuthenticated))
            {
                Log.Debug("Propagating list to portal {PortalClientId} reactively after an agent authenticated belonging to organization", portalConnection.ClientId);

                // Send the list of connected clients to the authenticated portal, we do that
                // by emulating a list command, so we have use the built-in ListBehavior

                try
                {
                    await listBehavior.ExecuteAsync(portalConnection, new EnvelopedMessage
                    {
                        From = envConfig.MachineName,
                        To = portalConnection.ClientId,
                        Type = MessageTypes.List.ToString().ToLowerInvariant(),
                        MessageId = Guid.NewGuid().ToString()
                    });
                }
                catch (Exception e)
                {
                    Log.Debug("Failed sending List to Portal connection, non critical: {e}", e);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("Failure during AfterAuthenticatedClientBehavior", e);
        }
    }
}