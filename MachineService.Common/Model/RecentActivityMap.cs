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
namespace MachineService.Common.Model;

/// <summary>
/// Helper class to keep track of recent activity for clients, to find out if a message is relevant for a gateway
/// </summary>
public class RecentActivityMap
{
    /// <summary>
    /// Lock for thread safety
    /// </summary>
    private readonly object _lock = new object();
    /// <summary>
    /// Dictionary to track recent activities for organization and client combinations
    /// </summary>
    private readonly Dictionary<string, DateTimeOffset> _recentActivities = new Dictionary<string, DateTimeOffset>();
    /// <summary>
    /// Timeout after which an activity is considered stale and removed from the map
    /// </summary>
    private static readonly TimeSpan ActivityTimeout = TimeSpan.FromMinutes(5);
    /// <summary>
    /// Last cleanup time to avoid frequent cleanups
    /// </summary>
    private DateTimeOffset _lastCleanup = DateTimeOffset.UtcNow + ActivityTimeout;
    /// <summary>
    /// The minimum size of the map before cleanup is considered
    /// </summary>
    private const int MinimumMapSize = 25;

    /// <summary>
    /// Generates a unique key for the organization and client combination
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="clientId">The client ID</param>
    /// <returns>A unique key string</returns>
    private static string GetKey(string organizationId, string clientId) => $"{organizationId}:{clientId}";

    /// <summary>
    /// Adds or updates the recent activity for the specified organization and client.
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="clientId">The client ID</param>
    public void AddOrUpdate(string organizationId, string clientId)
    {
        var key = GetKey(organizationId, clientId);
        lock (_lock)
        {
            _recentActivities[key] = DateTimeOffset.UtcNow;
            CleanupIfNeeded();
        }
    }

    /// <summary>
    /// Checks if there is recent activity for the specified organization and client.
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="clientId">The client ID</param>
    /// <returns>True if recent activity exists, otherwise false</returns>
    public bool Contains(string organizationId, string clientId)
    {
        var key = GetKey(organizationId, clientId);
        lock (_lock)
        {
            CleanupIfNeeded();
            return _recentActivities.ContainsKey(key);
        }
    }

    /// <summary>
    /// Cleans up stale entries from the recent activities map.
    /// </summary>
    private void CleanupIfNeeded()
    {
        if (_recentActivities.Count < MinimumMapSize)
            return;
        if (DateTimeOffset.UtcNow - _lastCleanup < ActivityTimeout)
            return;

        var expirationTime = DateTimeOffset.UtcNow - ActivityTimeout;
        var keysToRemove = _recentActivities.Where(kvp => kvp.Value < expirationTime).Select(kvp => kvp.Key).ToList();
        foreach (var key in keysToRemove)
            _recentActivities.Remove(key);

        _lastCleanup = DateTimeOffset.UtcNow;
    }
}
