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
using Microsoft.AspNetCore.Http;

namespace MachineService.Common.Util;

/// <summary>
/// Utility class for IP address operations.
/// </summary>
public static class IpUtils
{
    /// <summary>
    /// Gets the actual client IP address, considering X-Forwarded-For header for proxy scenarios.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The client IP address as a string, or null if not available.</returns>
    public static string? GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, take the first (original client)
            var ip = forwardedFor.Split(',').First().Trim();
            if (IPAddress.TryParse(ip, out _))
            {
                return ip;
            }
        }
        // Fallback to direct connection IP
        return context.Connection.RemoteIpAddress?.ToString();
    }
}