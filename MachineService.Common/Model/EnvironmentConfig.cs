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
using System.Reflection;

namespace MachineService.Common.Model;

/// <summary>
/// Represents the environment configuration
/// </summary>
/// <param name="MachineName">The machine name</param>
/// <param name="IsProd">Whether the environment is production</param>
/// <param name="VerifySchema">Whether to verify the database schema on startup</param>
/// <param name="MachineServerPrivate">The private key of the machine server</param>
/// <param name="MachineServerKeyExpires">The expiration date of the public key of the machine server</param>
/// <param name="Git_Commit_Version">The git commit version</param>
/// <param name="PreCompiledDbClasses">Whether the database classes are pre-compiled</param>
/// <param name="MaxBytesBeforeAuthentication">The maximum bytes before authentication</param>
/// <param name="WebsocketReceiveBufferSize">The websocket receive buffer size</param>
/// <param name="SecondsBetweenStatistics">Seconds between gathering statistics</param>
/// <param name="StatusReportIntervalSeconds">Interval in seconds between status reports to the backend</param>
/// <param name="RedirectUrl">The URL to redirect to, if requesting the root path</param>
/// <param name="DisableDatabaseClientHistory">Whether to disable client history tracking</param>
/// <param name="InMemoryClientList">Whether to use an in-memory client list instead of a database</param>
/// <param name="DisablePingMessages">Whether to disable ping activity messages sent on MassTransit</param>
/// <param name="DisableDatabaseStatistics">Whether to disable database statistics gathering</param>
public record EnvironmentConfig(
    string? MachineName,
    bool IsProd,
    bool? VerifySchema = null,
    string? MachineServerPrivate = null,
    string? RedirectUrl = null,
    DateTimeOffset? MachineServerKeyExpires = null,
    string? Git_Commit_Version = null,
    bool? PreCompiledDbClasses = false,
    int MaxBytesBeforeAuthentication = 100000,
    int WebsocketReceiveBufferSize = 4096,
    int SecondsBetweenStatistics = 300,
    int StatusReportIntervalSeconds = 30,
    bool DisableDatabaseClientHistory = false,
    bool InMemoryClientList = false,
    bool DisablePingMessages = false,
    bool DisableDatabaseStatistics = false
)
{
    /// <summary>
    /// Gets the Git version, falling back to assembly version if not set
    /// </summary>
    public string GitVersion => !string.IsNullOrWhiteSpace(Git_Commit_Version)
        ? Git_Commit_Version
        : (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())?.GetName().Version?.ToString() ?? "unknown";

    /// <summary>
    /// Flag indicating whether a database is required
    /// </summary>
    public bool RequiresDatabase => !InMemoryClientList || !DisableDatabaseStatistics;
}
