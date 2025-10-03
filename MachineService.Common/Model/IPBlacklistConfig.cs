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
/// The configuration for the IP Blacklist
/// </summary>
/// <param name="Storage">The KVPSButter connection string</param>
/// <param name="Entry">The key for the IP blacklist</param>
/// <param name="IPRulesCacheLifetimeSeconds">The lifetime of the IP rules cache in seconds</param>
public sealed record IPBlacklistConfig(
    string Storage,
    string Entry,
    int IPRulesCacheLifetimeSeconds = 60 * 60 * 24
)
{
    /// <summary>
    /// Checks if the configuration is valid
    /// </summary>
    /// <returns><c>true</c> if the configuration is valid; otherwise, <c>false</c>.</returns>
    public bool IsValid() => !string.IsNullOrWhiteSpace(Storage) && !string.IsNullOrWhiteSpace(Entry);
}
