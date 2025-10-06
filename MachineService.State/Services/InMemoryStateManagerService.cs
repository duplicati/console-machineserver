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

    /// <inheritdoc />
    public Task<bool> RegisterClient(ConnectionType clientType, Guid connectionId, string clientId, string organizationId, string? registeredAgentId, string? clientVersion, string machineServerUri, string? clientIp)
    {
        var orgDict = _registrations.GetOrAdd(organizationId, _ => new ConcurrentDictionary<string, ClientRegistration>());
        orgDict.AddOrUpdate(clientId, _ => new ClientRegistration()
        {
            ClientId = clientId,
            MachineServerUri = machineServerUri,
            OrganizationId = organizationId,
            MachineRegistrationId = registeredAgentId,
            ClientVersion = clientVersion,
            LastUpdatedOn = DateTimeOffset.UtcNow,
            Type = clientType,
        }, (_, existing) => existing with
        {
            LastUpdatedOn = DateTimeOffset.UtcNow,
            MachineServerUri = machineServerUri,
            MachineRegistrationId = registeredAgentId,
            ClientVersion = clientVersion,
            Type = clientType,
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

    /// <inheritdoc />
    public Task<List<ClientRegistration>> GetClients(string organizationId)
    {
        var orgDict = _registrations.GetOrAdd(organizationId, _ => new ConcurrentDictionary<string, ClientRegistration>());
        var registrations = orgDict.Values
            .Where(x => !x.ClientId.StartsWith("portal-client"))
            .ToList();
        return Task.FromResult(registrations);
    }
}
