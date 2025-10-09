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
/// Thread-safe list of active gateway connections
/// </summary>
public class GatewayConnectionList
{
    /// <summary>
    /// The list of active connections
    /// </summary>
    private readonly List<SocketState> _connections = new();

    /// <summary>
    /// Adds a new connection to the list
    /// </summary>
    /// <param name="connection">The connection to add</param>
    public void Add(SocketState connection)
    {
        lock (_connections)
            _connections.Add(connection);
    }

    /// <summary>
    /// Removes a connection from the list
    /// </summary>
    /// <param name="connection">The connection to remove</param>
    /// <returns>True if the connection was removed, false if it was not found</returns>
    public bool Remove(SocketState connection)
    {
        lock (_connections)
            return _connections.Remove(connection);
    }

    /// <summary>
    /// Finds all connections that are relevant to the specified organization and client
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="clientId">The client ID</param>
    /// <returns>A list of relevant connections</returns>
    public IEnumerable<SocketState> IsRelevantTo(string organizationId, string clientId)
    {
        lock (_connections)
            return _connections.Where(x => x.Authenticated && x.ConnectionState == ConnectionState.ConnectedGatewayAuthenticated && x.Type == ConnectionType.Gateway && x.IsInterestedIn(organizationId, clientId)).ToList();
    }

    /// <summary>
    /// Finds all connections that match the given predicate
    /// </summary>
    /// <param name="predicate">The predicate to match</param>
    /// <returns>>A list of matching connections</returns>
    public IEnumerable<SocketState> Where(Func<SocketState, bool> predicate)
    {
        lock (_connections)
            return _connections.Where(predicate).ToList();
    }

}
