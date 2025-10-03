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
using MachineService.Common.Enums;

namespace MachineService.Common.Services;

/// <summary>
/// Interface for thread-safe statistics gathering service.
/// </summary>
public interface IStatisticsGatherer
{
    /// <summary>
    /// Increments the counter for specified statistics type.
    /// </summary>
    /// <param name="statisticsType">The type of statistic to increment.</param>
    void Increment(StatisticsType statisticsType);

    /// <summary>
    /// Gets the current count for specified statistics type.
    /// </summary>
    /// <param name="statisticsType">The type of statistic to retrieve.</param>
    /// <returns>Current count for the specified statistics type.</returns>
    ulong GetCount(StatisticsType statisticsType);

    /// <summary>
    /// Exports current statistics and resets all counters to zero atomically.
    /// </summary>
    /// <returns>Dictionary containing current statistics before reset.</returns>
    Task<Dictionary<StatisticsType, ulong>> ExportAndResetAsync();

    /// <summary>
    /// Exports current statistics without resetting counters.
    /// </summary>
    /// <returns>Dictionary containing current statistics.</returns>
    Dictionary<StatisticsType, ulong> Export();
}