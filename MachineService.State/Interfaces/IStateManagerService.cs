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
using MachineService.Common.Model;
using MachineService.State.Model;

namespace MachineService.State.Interfaces;

/// <summary>
/// Interface for managing the state of the machinesservice clients
/// </summary>
public interface IStateManagerService
{
    /// <summary>
    /// Registers a client with the state manager
    /// </summary>
    /// <param name="clientType">The type of client</param>
    /// <param name="connectionId">The connection ID</param>
    /// <param name="clientId">The client ID</param>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="registeredAgentId">The registered agent ID</param>
    /// <param name="clientVersion">The client version</param>
    /// <param name="machineServer">The machine server URI</param>
    /// <param name="gatewayId">The gateway ID</param>
    /// <param name="clientIp">The client IP address</param>
    /// <returns><c>true</c> if the client was registered; otherwise, <c>false</c>.</returns>
    Task<bool> RegisterClient(ConnectionType clientType, Guid connectionId, string clientId, string organizationId, string? registeredAgentId, string? clientVersion, string? gatewayId, string? clientIp);

    /// <summary>
    /// Updates the activity timestamp for a client
    /// </summary>
    /// <param name="clientId">The client ID</param>
    /// <param name="organizationId">The organization ID</param>
    /// <returns><c>true</c> if the client activity was updated; otherwise,
    Task<bool> UpdateClientActivity(string clientId, string organizationId);

    /// <summary>
    /// Deregisters a client with the state manager
    /// </summary>
    /// <param name="connectionId">The connection ID</param>
    /// <param name="clientId">The client ID</param>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="bytesReceived">The number of bytes received</param>
    /// <param name="bytesSent">The number of bytes sent</param>
    /// <returns><c>true</c> if the client was deregistered; otherwise, <c>false</c>.</returns>
    Task<bool> DeRegisterClient(Guid connectionId, string clientId, string organizationId, long bytesReceived, long bytesSent);

    /// <summary>
    /// Gets the list of active agents for the specified organization
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <returns>A list of active agents</returns>
    public Task<List<ClientRegistration>> GetAgents(string organizationId);

    /// <summary>
    /// Gets the list of active portals for the specified organization
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <returns>A list of active portals</returns>
    public Task<List<ClientRegistration>> GetPortals(string organizationId);

    /// <summary>
    /// Cleans up stale data from the state manager
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task PurgeStaleData();
}