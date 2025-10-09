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
namespace MachineService.State.Interfaces;

/// <summary>
/// Interface for managing persisting statistics for the machineservice server
/// </summary>
public interface IStatisticsPersistenceService
{
    /// <summary>
    /// Persists the provided statistics to the database
    /// </summary>
    /// <param name="statistics">The statistics to persist</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns> A task representing the asynchronous operation</returns>
    Task PersistStatistics(Dictionary<string, ulong> statistics, CancellationToken cancellationToken);

    /// <summary>
    /// Cleans up stale data from the database
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task PurgeStaleData(CancellationToken cancellationToken);

}