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
namespace MachineService.Common.Services;

/// <summary>
/// Service to manage the list of active connections
/// </summary>
public class ConnectionListService
{
    /// <summary>
    /// The list of active connections
    /// </summary>
    private readonly List<SocketState> _connections = new();
    /// <summary>
    /// Lock object for thread safety
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Get a snapshot of the current connections
    /// </summary>
    /// <returns>A list of current connections</returns>
    public IEnumerable<SocketState> GetConnections()
    {
        lock (_lock)
            return _connections.ToList();
    }

    /// <summary>
    /// Add a new connection
    /// </summary>
    /// <param name="state">The socket state to add</param>
    public void Add(SocketState state)
    {
        lock (_lock)
            _connections.Add(state);
    }

    /// <summary>
    /// Remove a connection
    /// </summary>
    /// <param name="state">The socket state to remove</param>
    public void Remove(SocketState state)
    {
        lock (_lock)
            _connections.Remove(state);
    }

    /// <summary>
    /// Find the first connection that matches the given predicate
    /// </summary>
    /// <param name="predicate">The predicate to match</param>
    /// <returns>>The first matching connection, or null if none found</returns>
    public SocketState? FirstOrDefault(Func<SocketState, bool> predicate)
    {
        lock (_lock)
            return _connections.FirstOrDefault(predicate);
    }

}
