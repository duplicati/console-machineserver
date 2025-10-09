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
namespace MachineService.Common.Enums;

/// <summary>
/// Statistics enumeration
/// </summary>
public enum StatisticsType
{
    /// <summary>
    /// A command was successfully relayed to its destination
    /// </summary>
    CommandRelaySuccess = 100,
    /// <summary>
    /// The destination for a relayed command was not available
    /// </summary>
    CommandRelayDestinationNotAvailable = 101,
    /// <summary>
    /// Client authentication succeeded
    /// </summary>
    AuthClientSuccess = 102,
    /// <summary>
    /// Client authentication failed
    /// </summary>
    AuthClientFailure = 103,
    /// <summary>
    /// Portal authentication succeeded
    /// </summary>
    AuthPortalSuccess = 104,
    /// <summary>
    /// Portal authentication failed
    /// </summary>
    AuthPortalFailure = 105,
    /// <summary>
    /// List command succeeded
    /// </summary>
    ListCommandSuccess = 106,
    /// <summary>
    /// Ping command succeeded
    /// </summary>
    PingCommandSuccess = 107,
    /// <summary>
    /// Rate limit exceeded
    /// </summary>
    RateLimitExceeded = 108,
    /// <summary>
    /// Control relay initiated
    /// </summary>
    ControlRelayInitiated = 109,
    /// <summary>
    /// Control relay succeeded
    /// </summary>
    ControlRelaySuccess = 110,
    /// <summary>
    /// Control relay failure
    /// </summary>
    ControlRelayFailure = 111,
    /// <summary>
    /// Control relay destination not available
    /// </summary>
    ControlRelayDestinationNotAvailable = 112,
    /// <summary>
    /// Control relay destination not authenticated
    /// </summary>
    ControlRelayDestinationNotAuthenticated = 113,

    /// <summary>
    /// Client authentication timeout failure
    /// </summary>
    AuthClientTimeoutFailure = 114,
    /// <summary>
    /// Portal authentication timeout failure
    /// </summary>
    AuthPortalTimeoutFailure = 115,
    /// <summary>
    /// Client authentication failed due to client not found
    /// </summary>
    AuthClientNotFound = 116,
    /// <summary>
    /// Gateway authentication succeeded
    /// </summary>
    AuthGatewaySuccess = 117,
    /// <summary>
    /// Gateway authentication failed
    /// </summary>
    AuthGatewayFailure = 118,
    /// <summary>
    /// Invalid proxy command
    /// </summary>
    InvalidProxyCommand = 119,
}