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
using MachineService.Common.Model;

namespace MachineService.State.Model;

/// <summary>
/// Represents a client registration in the state manager
/// </summary>
public record ClientRegistration
{
    /// <summary>
    /// The client ID
    /// </summary>
    public required string ClientId { get; init; }
    /// <summary>
    /// The client version, if provided
    /// </summary>
    public required string? ClientVersion { get; init; }
    /// <summary>
    /// The organization ID
    /// </summary>
    public required string OrganizationId { get; init; }
    /// <summary>
    /// The machine registration ID, if provided
    /// </summary>
    public required string? MachineRegistrationId { get; init; }
    /// <summary>
    /// The machine server URI the client is connected to
    /// </summary>
    public required string MachineServerUri { get; init; }
    /// <summary>
    /// The connection type
    /// </summary>
    public required ConnectionType Type { get; init; }
    /// <summary>
    /// The last updated timestamp
    /// </summary>
    public DateTimeOffset LastUpdatedOn { get; init; }
}