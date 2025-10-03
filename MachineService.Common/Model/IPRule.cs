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
using System.Net;
using System.Text.Json.Serialization;

namespace MachineService.Common.Model;

/// <summary>
/// Represents an IP Rule for denying access from a specific IP address or range.
/// </summary>
public sealed record IPRule()
{
    /// <summary>
    /// The CIDR notation for the IP address or range (e.g., "192.168.1.0/24"), or a single IP address (e.g., "192.168.1.1")
    /// </summary>
    [JsonPropertyName("cidr")]
    public required string CIDR { get; init; }

    /// <summary>
    /// An optional description of the rule
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; } = null;

    /// <summary>
    /// Checks if the rule is valid
    /// </summary>
    /// <returns><c>true</c> if the rule is valid; otherwise, <c>false</c>.</returns>
    public bool IsValid() => !string.IsNullOrWhiteSpace(CIDR)
        && (IPAddress.TryParse(CIDR, out _) || (CIDR.Contains('/') && CIDR.Split('/').Length == 2 && IPAddress.TryParse(CIDR.Split('/')[0], out _)));

    /// <summary>
    /// Checks if the given IP address matches this rule.
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <returns><c>true</c> if the IP address matches the rule; otherwise, <c>false</c>.</returns>
    public bool IsMatch(IPAddress ipAddress)
    {
        if (!CIDR.Contains('/'))
            return IPAddress.TryParse(CIDR, out var ruleIp) && ruleIp.Equals(ipAddress);

        var parts = CIDR.Split('/');
        return int.TryParse(parts[1], out _) &&
               IPNetwork.Parse(CIDR).Contains(ipAddress);
    }
}
