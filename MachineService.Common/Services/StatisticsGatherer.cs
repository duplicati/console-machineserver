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
using MachineService.Common.Enums;

namespace MachineService.Common.Services;

/// <summary>
/// Thread-safe service for gathering and tracking statistics across different operation types.
/// </summary>
public class StatisticsGatherer : IStatisticsGatherer
{
    /// <summary>
    /// Dictionary to hold statistics counts in a thread-safe manner.
    /// </summary>
    private ConcurrentDictionary<StatisticsType, ulong> _statistics = InitializeStatistics();

    /// <summary>
    /// Semaphore to ensure atomic operations during export and reset.
    /// </summary>
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Initializes a new ConcurrentDictionary with all StatisticsType enum values set to 0.
    /// </summary>
    private static ConcurrentDictionary<StatisticsType, ulong> InitializeStatistics()
        => new ConcurrentDictionary<StatisticsType, ulong>(
            Enum.GetValues(typeof(StatisticsType))
                .Cast<StatisticsType>()
                .ToDictionary(k => k, v => 0UL));

    /// <summary>
    /// Exports current statistics and resets all counters to zero atomically.
    /// </summary>
    /// <returns>Dictionary containing current statistics before reset.</returns>
    public async Task<Dictionary<StatisticsType, ulong>> ExportAndResetAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var snapshot = Export();
            _statistics = InitializeStatistics();
            return snapshot;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Exports current statistics without resetting counters.
    /// </summary>
    /// <returns>Dictionary containing current statistics.</returns>
    public Dictionary<StatisticsType, ulong> Export()
        => _statistics.ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>
    /// Increments the counter for specified statistics type in a thread-safe manner.
    /// </summary>
    /// <param name="statisticsType">The type of statistic to increment.</param>
    public void Increment(StatisticsType statisticsType)
    {
        _statistics.AddOrUpdate(
            statisticsType,
            1UL,
            (_, oldValue) => oldValue + 1
        );
    }

    /// <summary>
    /// Gets the current count for specified statistics type.
    /// </summary>
    /// <param name="statisticsType">The type of statistic to retrieve.</param>
    /// <returns>Current count for the specified statistics type.</returns>
    public ulong GetCount(StatisticsType statisticsType)
        => _statistics[statisticsType];
}