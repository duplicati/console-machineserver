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

namespace MachineService.Server.Services;

/// <summary>
/// Service that periodically persists the statistics gathered to database.
/// </summary>
/// <param name="environmentConfig">Injected singleton instance of environment configuration</param>
/// <param name="provider">Service provider for creating scoped services</param>
public class GatherStatisticsPersistence(EnvironmentConfig environmentConfig, IServiceScopeFactory provider) : BackgroundService
{
    /// <summary>
    /// Service execution implementation
    /// </summary>
    /// <param name="stoppingToken"></param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = TimeSpan.FromSeconds(environmentConfig.SecondsBetweenStatistics);
        using var timer = new PeriodicTimer(period);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = provider.CreateScope();
                    var statistics = scope.ServiceProvider.GetRequiredService<IStatisticsGatherer>().Export();

                    foreach (var kvp in statistics)
                        Console.WriteLine($"{kvp.Key,-40}\t{kvp.Value}");

                    await scope.ServiceProvider.GetRequiredService<IStatisticsPersistenceService>()
                        .PersistStatistics(statistics.ToDictionary(k => k.Key.ToString(), k => k.Value), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error in persisting statistics");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful exit
        }
    }
}