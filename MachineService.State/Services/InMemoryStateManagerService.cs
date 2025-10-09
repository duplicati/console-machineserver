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
using System.Collections.Concurrent;
using MachineService.Common.Model;
using MachineService.State.Interfaces;
using MachineService.State.Model;

namespace MachineService.State.Services;

/// <summary>
/// In-memory implementation of the state manager service for development and testing purposes.
/// </summary>
public class InMemoryStateManagerService : IStateManagerService
{
    /// <summary>
    /// The in-memory list of client registrations.
    /// Outher dictionary key is organization ID
    /// Inner dictionary key is client ID
    /// </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ClientRegistration>> _registrations = new ConcurrentDictionary<string, ConcurrentDictionary<string, ClientRegistration>>();

    /// <summary>
    /// The timeout after which a client is considered inactive and removed from the list.
    /// </summary>
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromMinutes(30);

    /// <inheritdoc />
    public Task<bool> RegisterClient(ConnectionType clientType, Guid connectionId, string clientId, string organizationId, string? registeredAgentId, string? clientVersion, string? gatewayId, string? clientIp)
    {
        var orgDict = _registrations.GetOrAdd(organizationId, _ => new ConcurrentDictionary<string, ClientRegistration>());
        orgDict.AddOrUpdate(clientId, _ => new ClientRegistration()
        {
            ClientId = clientId,
            OrganizationId = organizationId,
            MachineRegistrationId = registeredAgentId,
            ClientVersion = clientVersion,
            LastUpdatedOn = DateTimeOffset.UtcNow,
            Type = clientType,
            GatewayId = gatewayId ?? "",
        }, (_, existing) => existing with
        {
            LastUpdatedOn = DateTimeOffset.UtcNow,
            MachineRegistrationId = registeredAgentId,
            ClientVersion = clientVersion,
            Type = clientType,
            GatewayId = gatewayId ?? ""
        });

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> UpdateClientActivity(string clientId, string organizationId)
    {
        var orgDict = _registrations.GetOrAdd(organizationId, _ => new ConcurrentDictionary<string, ClientRegistration>());

        // Not found, ignore
        if (!orgDict.TryGetValue(clientId, out var existing))
            return Task.FromResult(false);

        // Update the existing registration with a new timestamp
        var updated = existing with { LastUpdatedOn = DateTimeOffset.UtcNow };

        // If this failsed, someone else updated it first, which is fine because we just want to update the timestamp
        orgDict.TryUpdate(clientId, updated, existing);

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> DeRegisterClient(Guid connectionId, string clientId, string organizationId, long bytesReceived, long bytesSent)
    {
        var orgDict = _registrations.GetOrAdd(organizationId, _ => new ConcurrentDictionary<string, ClientRegistration>());
        orgDict.TryRemove(clientId, out _);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Gets the list of active connections for the specified organization and client type.
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="clientType">The client type</param>
    /// <returns>A list of active connections</returns>
    public Task<List<ClientRegistration>> GetConnections(string organizationId, ConnectionType clientType)
    {
        var orgDict = _registrations.GetOrAdd(organizationId, _ => new ConcurrentDictionary<string, ClientRegistration>());
        var registrations = orgDict.Values
            .Where(x => x.Type == clientType)
            .Where(x => x.LastUpdatedOn >= DateTimeOffset.UtcNow - ClientTimeout)
            .ToList();
        return Task.FromResult(registrations);
    }

    /// <inheritdoc />
    public async Task<List<ClientRegistration>> GetAgents(string organizationId)
        => await GetConnections(organizationId, ConnectionType.Agent);

    /// <inheritdoc />
    public async Task<List<ClientRegistration>> GetPortals(string organizationId)
        => await GetConnections(organizationId, ConnectionType.Portal);

    /// <inheritdoc />
    public Task PurgeStaleData()
    {
        var expires = DateTimeOffset.UtcNow - ClientTimeout;
        foreach (var orgDict in _registrations.Values)
        {
            var toRemove = orgDict.Where(kvp => kvp.Value.LastUpdatedOn < expires).Select(kvp => kvp.Key).ToList();
            foreach (var key in toRemove)
                orgDict.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }

}
