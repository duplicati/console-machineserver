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
using Marten;
using Serilog;
using UUIDNext;

namespace MachineService.State.Services;

/// <summary>
/// Implementation for managing persisting statistics for the machineservice server
/// </summary>
/// <param name="config">The environment configuration</param>
/// <param name="store">The MartenDB document store</param>
public class StatisticsPersistenceService(EnvironmentConfig config, IDocumentStore store) : IStatisticsPersistenceService
{
    /// <summary>
    /// The record for storing statistics entries
    /// </summary>
    /// <param name="Id">The unique ID of the entry</param>
    /// <param name="CreatedAt">When the statistics were created</param>
    /// <param name="Statistics">The statistics data</param>
    public sealed record StatisticsEntry(string Id, DateTime CreatedAt, Dictionary<string, ulong> Statistics);

    /// <inheritdoc/>
    public async Task PersistStatistics(Dictionary<string, ulong> statistics)
    {
        if (statistics == null || statistics.Count == 0)
            return;

        var entry = new StatisticsEntry(Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(), DateTime.UtcNow, statistics);
        using var session = store.LightweightSession();
        session.Insert(entry);
        try
        {
            await session.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error persisting statistics: {Error}");
        }
    }

    /// <inheritdoc/>
    public Task PurgeStaleData()
    {
        var expirationTime = DateTimeOffset.UtcNow - TimeSpan.FromDays(config.StatisticsRetentionDays);
        using var session = store.LightweightSession();
        session.DeleteWhere<StatisticsEntry>(x => x.CreatedAt < expirationTime);
        return session.SaveChangesAsync();
    }

    /// <summary>
    /// Configures the MartenDB store options for the statistics persistence
    /// </summary>
    /// <param name="options">The MartenDB store options</param>
    /// <returns>The configured store options</returns>
    public static StoreOptions ConfigureDatabase(StoreOptions options)
    {
        options.RegisterDocumentType<StatisticsEntry>();
        options.Schema.For<StatisticsEntry>()
            .DocumentAlias("machineservices_statistics")
            .Identity(x => x.Id)
            .UniqueIndex(x => x.CreatedAt);
        return options;
    }
}