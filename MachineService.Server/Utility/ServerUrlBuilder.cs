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

namespace MachineService.Server.Utility;

/// <summary>
/// Utility to build the server URL to be used by gateway to reach the clients.
/// </summary>
public static class ServerUrlBuilder
{
#if DEBUG
    private static bool _useWss = false;
#else
    private static bool _useWss = true;
#endif

    public static string BuildUrl()
    {
        var ipAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList
            .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                  && !ip.ToString().Equals("127.0.0.1")
                                  && !ip.ToString().Equals("0.0.0.0"))
            ?.ToString();
        if (ipAddress == null)
        {
            Log.Warning($"Could not determine IP address for server URL, will return {Dns.GetHostName()}");
            ipAddress = Dns.GetHostName();
        }
        return _useWss ? $"wss://{ipAddress}/gateway" : $"ws://{ipAddress}/gateway";
    }
}