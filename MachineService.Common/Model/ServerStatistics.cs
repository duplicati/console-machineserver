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
namespace MachineService.Server.Model;

/// <summary>
/// Class used to keep thread-safe server statistics.
/// </summary>
public class ServerStatistics
{
    /// <summary>
    /// Total number of connections since server start.
    /// </summary>
    private ulong _totalConnections;
    /// <summary>
    /// Total number of messages received since server start.
    /// </summary>
    private ulong _totalMessagesReceived;
    /// <summary>
    /// Maximum number of simultaneous connections observed.
    /// </summary>
    private int _maxSimultaneousConnections;

    /// <summary>
    /// Total number of messages received since server start.
    /// </summary>
    public ulong TotalMessagesReceived => _totalMessagesReceived;
    /// <summary>
    /// Total number of connections since server start.
    /// </summary>
    public ulong TotalConnections => _totalConnections;
    /// <summary>
    /// Maximum number of simultaneous connections observed.
    /// </summary>
    public int MaxSimultaneousConnections => _maxSimultaneousConnections;
    /// <summary>
    /// The time the server was started (UTC).
    /// </summary>
    private DateTime Started { get; } = DateTime.UtcNow;

    /// <summary>
    /// Returns a human-readable string of server uptime.
    /// </summary>
    public string UptimeVerbose
    {
        get
        {
            var uptime = DateTime.UtcNow - Started;
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        }
    }

    /// <summary>
    /// Thread-safe increment of total connections with overflow protection.
    /// Resets to 0 when approaching ulong.MaxValue to prevent overflow.
    /// </summary>
    public void IncrementConnectionCount()
    {
        // This - 1024 is to create a buffer zone to avoid a race condition on the very edge case.
        if (_totalConnections > ulong.MaxValue - 1024)
            Interlocked.Exchange(ref _totalConnections, 0);
        else
            Interlocked.Increment(ref _totalConnections);
    }

    /// <summary>
    /// Thread-safe increment of messages received with overflow protection.
    /// Resets to 0 when approaching ulong.MaxValue to prevent overflow.
    /// </summary>
    public void IncrementMessagesReceivedCount()
    {
        // This - 1024 is to create a buffer zone to avoid a race condition on the very edge case.
        if (_totalMessagesReceived > ulong.MaxValue - 1024)
            Interlocked.Exchange(ref _totalMessagesReceived, 0);
        else
            Interlocked.Increment(ref _totalMessagesReceived);
    }

    /// <summary>
    /// Thread-safe update of maximum simultaneous connections count.
    /// Uses atomic compare-exchange to track the highest concurrent connection count.
    /// </summary>
    public void RecordCurrentConnections(int currentConnections)
    {
        Interlocked.CompareExchange(
            ref _maxSimultaneousConnections,
            Math.Max(currentConnections, _maxSimultaneousConnections),
            _maxSimultaneousConnections);
    }
}