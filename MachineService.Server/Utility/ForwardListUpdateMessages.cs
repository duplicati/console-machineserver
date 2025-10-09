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
using MachineService.State.Interfaces;

namespace MachineService.Server.Utility;

/// <summary>
/// Helper class to forward list update messages to relevant gateways and connected portals
/// </summary>
public static class ForwardListUpdateMessages
{
    /// <summary>
    /// Forward the list update message to all relevant gateways
    /// </summary>
    /// <param name="gatewayConnectionList">The gateway connection list</param>
    /// <param name="stateManagerService">The state manager service</param>
    /// <param name="instanceId">The instance ID</param>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async Task ForwardListUpdateToRelevantGateways(GatewayConnectionList gatewayConnectionList, IStateManagerService stateManagerService, string instanceId, string organizationId, CancellationToken cancellationToken)
    {
        foreach (var portalInstance in await stateManagerService.GetPortals(organizationId, cancellationToken))
        {
            foreach (var gatewayServer in gatewayConnectionList.Where(x => x.ClientId == portalInstance.GatewayId))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var message = new EnvelopedMessage
                    {
                        From = instanceId,
                        To = gatewayServer.ClientId,
                        Type = MessageTypes.Proxy.ToString().ToLowerInvariant(),
                        MessageId = Guid.NewGuid().ToString(),
                        Payload = EnvelopedMessage.SerializePayload(new ProxyMessage
                        {
                            Type = MessageTypes.List.ToString().ToLowerInvariant(),
                            From = instanceId,
                            To = gatewayServer.ClientId!,
                            OrganizationId = organizationId,
                            InnerMessage = null
                        })
                    };

                    await gatewayServer.WriteMessage(message, WrappingType.PlainText);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Failed to forward list update to gateway {GatewayId}", gatewayServer.ClientId);
                }
            }
        }
    }

    /// <summary>
    /// Forward the list update message to all connected and authenticated portals
    /// </summary>
    /// <param name="connectionListService">The connection list service</param>
    /// <param name="listBehavior">The list behavior to execute</param>
    /// <param name="instanceId">The instance ID</param>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async Task ForwardListUpdateToConnectedClients(this ConnectionListService connectionListService, ListBehavior listBehavior, string instanceId, string organizationId, CancellationToken cancellationToken)
    {
        // Proactively send the list of connected clients to all existing and authenticated
        // portal connections
        var listSnapshot = connectionListService.GetConnections();

        foreach (var portalConnection in listSnapshot.Where(x =>
                     x.OrganizationId == organizationId &&
                     x.ConnectionState == ConnectionState.ConnectedPortalAuthenticated))
        {
            Log.Debug("Propagating list to portal {PortalClientId} reactively after an agent connected/disconnected belonging to organization {OrganizationId}", portalConnection.ClientId, organizationId);

            // Send the list of connected clients to the authenticated portal, we do that
            // by emulating a list command, so we have use the built-in ListBehavior

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await listBehavior.ExecuteAsync(portalConnection, new EnvelopedMessage
                {
                    From = instanceId,
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
}
