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
using Serilog;
using UUIDNext;

namespace MachineService.State.Services;

/// <summary>
/// State manager service implementation using MartenDB as the backing store
/// </summary>
/// <param name="store">The MartenDB document store</param>
/// <param name="environmentConfig">The environment configuration</param>
public class StateManagerService(IDocumentStore store, EnvironmentConfig environmentConfig) : IStateManagerService
{
    /// <summary>
    /// The retention period for active connections
    /// </summary>
    private static readonly TimeSpan ConnectionRetentionPeriod = TimeSpan.FromDays(1);

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
    /// The instance for storing active connections
    /// </summary>
    public sealed class ActiveConnection
    {
        /// <summary>
        /// The unique identifier for the active connection
        /// </summary>
        public required string Id { get; set; }
        /// <summary>
        /// The gateway ID
        /// </summary>
        public required string? GatewayId { get; set; }
        /// <summary>
        /// The client ID
        /// </summary>
        public required string ClientId { get; set; }
        /// <summary>
        /// The organization ID
        /// </summary>
        public required string OrganizationId { get; set; }
        /// <summary>
        /// The first seen timestamp
        /// </summary>
        public required DateTimeOffset FirstSeenOn { get; set; }
        /// <summary>
        /// The last update timestamp
        /// </summary>
        public required DateTimeOffset LastUpdateOn { get; set; }
        /// <summary>
        /// The machine registration ID
        /// </summary>
        public required string? MachineRegistrationId { get; set; }
        /// <summary>
        /// The client version
        /// </summary>
        public required string? ClientVersion { get; set; }
        /// <summary>
        /// The client IP address
        /// </summary>
        public required string? ClientIp { get; set; }
        /// <summary>
        /// The client type
        /// </summary>
        public required ConnectionType ClientType { get; set; }
    }

    /// <summary>
    /// The record for storing client registration history
    /// </summary>
    /// <param name="RegisterId">The registration ID</param>
    /// <param name="ClientId">The client ID</param>
    /// <param name="GatewayId">The gateway ID</param>
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
        string? GatewayId,
        RegisterAction Action,
        string OrganizationId,
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

    /// <summary>
    /// The timeout after which a client is considered inactive and removed from the list.
    /// </summary>
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public async Task<bool> RegisterClient(ConnectionType clientType, Guid connectionId, string clientId, string organizationId,
        string? registeredAgentId, string? clientVersion, string? gatewayId, string? clientIp,
        CancellationToken cancellationToken)
    {
        using (var session = store.LightweightSession())
        {
            var current = await session.Query<ActiveConnection>()
                .FirstOrDefaultAsync(x => x.ClientId == clientId && x.OrganizationId == organizationId, cancellationToken);

            if (current == null)
            {
                current = new ActiveConnection()
                {
                    Id = Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(),
                    ClientId = clientId,
                    GatewayId = gatewayId,
                    OrganizationId = organizationId,
                    FirstSeenOn = DateTimeOffset.UtcNow,
                    LastUpdateOn = DateTimeOffset.UtcNow,
                    MachineRegistrationId = registeredAgentId,
                    ClientVersion = clientVersion,
                    ClientIp = clientIp,
                    ClientType = clientType
                };
            }
            else
            {
                current.ClientId = clientId;
                current.GatewayId = gatewayId;
                current.OrganizationId = organizationId;
                current.LastUpdateOn = DateTimeOffset.UtcNow;
                current.MachineRegistrationId = registeredAgentId;
                current.ClientVersion = clientVersion;
                current.ClientIp = clientIp;
                current.ClientType = clientType;
            }

            session.Store(current);
            await session.SaveChangesAsync(cancellationToken);
        }

        if (!environmentConfig.DisableDatabaseClientHistory)
        {
            try
            {

                using var historySession = store.LightweightSession();
                historySession.Store(new ClientRegisterHistory(
                    RegisterId: Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(),
                    ClientId: clientId,
                    GatewayId: gatewayId,
                    Action: RegisterAction.Register,
                    OrganizationId: organizationId,
                    RegisteredOn: DateTimeOffset.UtcNow,
                    MachineRegistrationId: registeredAgentId,
                    ClientVersion: clientVersion,
                    ConnectionId: connectionId.ToString(),
                    ClientIp: clientIp,
                    ClientType: clientType));
                await historySession.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to log client registration history for client {ClientId} in organization {OrganizationId}", clientId, organizationId);
            }
        }
        return true;
    }


    /// <inheritdoc />
    public async Task<bool> UpdateClientActivity(string clientId, string organizationId, CancellationToken cancellationToken)
    {
        try
        {
            using var session = store.LightweightSession();
            var current = await session.Query<ActiveConnection>()
                .FirstOrDefaultAsync(x => x.ClientId == clientId && x.OrganizationId == organizationId, cancellationToken);

            if (current == null)
                return false;

            current.LastUpdateOn = DateTimeOffset.UtcNow;

            session.Store(current);
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update client activity for client {ClientId} in organization {OrganizationId}", clientId, organizationId);
            return false;
        }
        return true;
    }


    /// <inheritdoc />
    public async Task<bool> DeRegisterClient(Guid connectionId, string clientId, string organizationId, long bytesReceived, long bytesSent, CancellationToken cancellationToken)
    {
        var result = true;
        try
        {
            using var session = store.LightweightSession();
            if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(organizationId))
                session.DeleteWhere<ActiveConnection>(x => x.ClientId == clientId && x.OrganizationId == organizationId);
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (ObjectDisposedException ex)
        {
            Log.Warning(ex, "Database pool disposed during deregistration for client {ClientId} - application shutting down", clientId);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to deregister client {ClientId} in organization {OrganizationId}", clientId, organizationId);
            result = false;
        }

        if (!environmentConfig.DisableDatabaseClientHistory)
        {
            try
            {
                using var historySession = store.LightweightSession();
                historySession.Store(new ClientUnregisterHistory(
                    UnregisterId: Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(),
                    ClientId: clientId,
                    UnregisterOn: DateTimeOffset.UtcNow,
                    BytesReceived: bytesReceived,
                    BytesSent: bytesSent,
                    ConnectionId: connectionId.ToString()));
                await historySession.SaveChangesAsync(cancellationToken);
            }
            catch (ObjectDisposedException ex)
            {
                Log.Debug(ex, "Database pool disposed during history logging - application shutting down");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to log client unregistration history for client {ClientId} in organization {OrganizationId}", clientId, organizationId);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the list of active connections for the specified organization and client type
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="clientType">The client type</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A list of active connections</returns>
    private async Task<List<ClientRegistration>> GetConnections(string organizationId, ConnectionType clientType, CancellationToken cancellationToken)
    {
        var expirationTime = DateTimeOffset.UtcNow - ClientTimeout;
        using var session = store.LightweightSession();
        var registrations = await session.Query<ActiveConnection>()
            .Where(x => x.OrganizationId == organizationId)
            .Where(x => x.LastUpdateOn >= expirationTime)
            .Where(x => x.ClientType == clientType)
            .ToListAsync(cancellationToken);

        return registrations.Select(x => new ClientRegistration()
        {
            GatewayId = x.GatewayId ?? "",
            ClientId = x.ClientId,
            OrganizationId = x.OrganizationId,
            MachineRegistrationId = x.MachineRegistrationId,
            ClientVersion = x.ClientVersion,
            LastUpdatedOn = x.LastUpdateOn,
            Type = x.ClientType
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<List<ClientRegistration>> GetAgents(string organizationId, CancellationToken cancellationToken)
        => await GetConnections(organizationId, ConnectionType.Agent, cancellationToken);

    /// <inheritdoc />
    public async Task<List<ClientRegistration>> GetPortals(string organizationId, CancellationToken cancellationToken)
        => await GetConnections(organizationId, ConnectionType.Portal, cancellationToken);

    /// <inheritdoc />
    public async Task PurgeStaleData(CancellationToken cancellationToken)
    {
        using var session = store.LightweightSession();

        var expirationTimeConnections = DateTimeOffset.UtcNow - ConnectionRetentionPeriod;
        session.DeleteWhere<ActiveConnection>(x => x.LastUpdateOn < expirationTimeConnections);

        if (!environmentConfig.DisableDatabaseClientHistory)
        {
            var expirationTimeActivityLog = DateTimeOffset.UtcNow - TimeSpan.FromDays(environmentConfig.StatisticsRetentionDays); ;
            session.DeleteWhere<ClientRegisterHistory>(x => x.RegisteredOn < expirationTimeActivityLog);
            session.DeleteWhere<ClientUnregisterHistory>(x => x.UnregisterOn < expirationTimeActivityLog);
        }
        await session.SaveChangesAsync(cancellationToken);
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