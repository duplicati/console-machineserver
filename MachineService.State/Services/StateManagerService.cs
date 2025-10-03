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
using MachineService.State.Interfaces;
using MachineService.State.Model;
using Marten;
using UUIDNext;

namespace MachineService.State.Services;

/// <summary>
/// State manager service implementation using MartenDB as the backing store
/// </summary>
/// <param name="session">The MartenDB document session</param>
public class StateManagerService(IDocumentStore store) : IStateManagerService
{
    /// <summary>
    /// The action taken when registering or unregistering a client
    /// </summary>
    public enum RegisterAction
    {
        /// <summary>
        /// Registering a client
        /// </summary>
        Register,
        /// <summary>
        /// Unregistering a client
        /// </summary>
        Unregister
    }

    /// <summary>
    /// The record for storing active connections
    /// </summary>
    /// <param name="ClientId">The client ID</param>
    /// <param name="MachineServerUri">The machine server URI</param>
    /// <param name="OrganizationId">The organization ID</param>
    /// <param name="FirstSeenOn">The first seen timestamp</param>
    /// <param name="LastUpdateOn">The last update timestamp</param>
    /// <param name="MachineRegistrationId">The machine registration ID</param>
    /// <param name="ClientVersion">The client version</param>
    /// <param name="ClientIp">The client IP address</param>
    /// <param name="ClientType">The client type</param>
    public sealed record ActiveConnection(
        string Id,
        string ClientId,
        string MachineServerUri,
        string OrganizationId,
        DateTimeOffset FirstSeenOn,
        DateTimeOffset LastUpdateOn,
        string? MachineRegistrationId,
        string? ClientVersion,
        string? ClientIp,
        ConnectionType ClientType);


    /// <summary>
    /// The record for storing client registration history
    /// </summary>
    /// <param name="RegisterId">The registration ID</param>
    /// <param name="ClientId">The client ID</param>
    /// <param name="Action">The action taken (register/unregister)</param>
    /// <param name="OrganizationId">The organization ID</param>
    /// <param name="MachineServerUri">The machine server URI</param>
    /// <param name="RegisteredOn">The registered timestamp</param>
    /// <param name="MachineRegistrationId">The machine registration ID</param>
    /// <param name="ClientVersion">The client version</param>
    /// <param name="ConnectionId">The connection ID</param>
    /// <param name="ClientIp">The client IP address</param>
    /// <param name="ClientType">The client type</param>
    public sealed record ClientRegisterHistory(
        string RegisterId,
        string ClientId,
        RegisterAction Action,
        string OrganizationId,
        string MachineServerUri,
        DateTimeOffset RegisteredOn,
        string? MachineRegistrationId,
        string? ClientVersion,
        string ConnectionId,
        string? ClientIp,
        ConnectionType ClientType);

    /// <summary>
    /// The record for storing client unregistration history
    /// </summary>
    /// <param name="UnregisterId">The unregistration ID</param>
    /// <param name="ClientId">The client ID</param>
    /// <param name="UnregisterOn">The unregistration timestamp</param>
    /// <param name="BytesReceived">The number of bytes received</param>
    /// <param name="BytesSent">The number of bytes sent</param>
    /// <param name="ConnectionId">The connection ID</param>
    public sealed record ClientUnregisterHistory(
        string UnregisterId,
        string ClientId,
        DateTimeOffset UnregisterOn,
        long BytesReceived,
        long BytesSent,
        string ConnectionId);

    /// <inheritdoc />
    public async Task<bool> RegisterClient(ConnectionType clientType, Guid connectionId, string clientId, string organizationId,
        string? registeredAgentId, string? clientVersion, string machineServerUri, string? clientIp)
    {
        using var session = store.LightweightSession();
        var current = session.Query<ActiveConnection>()
            .FirstOrDefault(x => x.ClientId == clientId && x.OrganizationId == organizationId);

        session.Store(new ActiveConnection(
            Id: current?.Id ?? Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(),
            ClientId: clientId,
            MachineServerUri: machineServerUri,
            OrganizationId: organizationId,
            FirstSeenOn: current?.FirstSeenOn ?? DateTimeOffset.UtcNow,
            LastUpdateOn: DateTimeOffset.UtcNow,
            MachineRegistrationId: registeredAgentId,
            ClientVersion: clientVersion,
            ClientIp: clientIp,
            ClientType: clientType
        ));
        session.Store(new ClientRegisterHistory(
            RegisterId: Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(),
            ClientId: clientId,
            Action: RegisterAction.Register,
            OrganizationId: organizationId,
            MachineServerUri: machineServerUri,
            RegisteredOn: DateTimeOffset.UtcNow,
            MachineRegistrationId: registeredAgentId,
            ClientVersion: clientVersion,
            ConnectionId: connectionId.ToString(),
            ClientIp: clientIp,
            ClientType: clientType));

        await session.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeRegisterClient(Guid connectionId, string clientId, string organizationId, long bytesReceived, long bytesSent)
    {
        using var session = store.LightweightSession();
        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(organizationId))
            session.DeleteWhere<ActiveConnection>(x => x.ClientId == clientId && x.OrganizationId == organizationId);

        session.Store(new ClientUnregisterHistory(
            UnregisterId: Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(),
            ClientId: clientId,
            UnregisterOn: DateTimeOffset.UtcNow,
            BytesReceived: bytesReceived,
            BytesSent: bytesSent,
            ConnectionId: connectionId.ToString()));

        await session.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<List<ClientRegistration>> GetClients(string organizationId)
    {
        using var session = store.LightweightSession();
        var registrations = await session.Query<ActiveConnection>()
            .Where(x => x.OrganizationId == organizationId && !x.ClientId.StartsWith("portal-client", StringComparison.OrdinalIgnoreCase))
            .ToListAsync();

        return registrations.Select(x => new ClientRegistration()
        {
            ClientId = x.ClientId,
            MachineServerUri = x.MachineServerUri,
            OrganizationId = x.OrganizationId,
            MachineRegistrationId = x.MachineRegistrationId,
            ClientVersion = x.ClientVersion,
            LastUpdatedOn = x.LastUpdateOn,
            Type = x.ClientType,
        }).ToList();
    }

    /// <summary>
    /// Configure the MartenDB document store
    /// </summary>
    /// <param name="options">The store options</param>
    /// <returns>The configured store options</returns>
    public static StoreOptions ConfigureDatabase(StoreOptions options)
    {
        options.RegisterDocumentType<ActiveConnection>();
        options.Schema.For<ActiveConnection>()
            .DocumentAlias("machineservices_activeconnection")
            .Identity(x => x.Id)
            .UniqueIndex("machineservices_activeconnection_clientid_organizationid", x => x.ClientId, x => x.OrganizationId);

        options.RegisterDocumentType<ClientRegisterHistory>();
        options.Schema.For<ClientRegisterHistory>()
            .DocumentAlias("machineservices_clientregisterhistory")
            .Identity(x => x.RegisterId);

        options.RegisterDocumentType<ClientUnregisterHistory>();
        options.Schema.For<ClientUnregisterHistory>()
            .DocumentAlias("machineservices_clientunregisterhistory")
            .Identity(x => x.UnregisterId);

        return options;
    }
}