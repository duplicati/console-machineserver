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
using System.Diagnostics;
using Interprocess.NamedPipes;

string namedPipeName = "machineservice";

async Task<int> HandleTrace(string clientId)
{
    var client = new NamedPipeClient(namedPipeName);

    if (clientId is "off" or "reset")
    {
        Console.WriteLine("Sending trace reset command");
        var response = await client.SendMessageAsync("trace", "reset");
        Console.WriteLine($"Result {response.Success}. {response.message}");
        return response.Success ? 0 : 1;
    }
    else
    {
        Console.WriteLine("Sending trace client command");
        var response = await client.SendMessageAsync("trace", clientId);
        Console.WriteLine($"Result {response.Success}. {response.message}");
        return response.Success ? 0 : 1;
    }
}

async Task<int> HandleLogLevel(string level)
{
    var client = new NamedPipeClient(namedPipeName);
    if (level == "reset")
    {
        Console.WriteLine("Resetting log level to default");
        var response = await client.SendMessageAsync("loglevel", "reset");
        Console.WriteLine($"Result {response.Success}. {response.message}");
        return response.Success ? 0 : 1;
    }

    if (!new[] { "debug", "information", "warning", "error", "fatal" }.Contains(level.ToLower()))
    {
        Console.WriteLine("Invalid log level. Valid options are: debug, information, warning, error, fatal");
        return 1;
    }
    else
    {
        var response = await client.SendMessageAsync("loglevel", level.ToLower());
        Console.WriteLine($"Result {response.Success}. {response.message}");
        return response.Success ? 0 : 1;
    }
}

return args switch
{
    [] => ShowUsageAndReturn(),
    ["trace", var clientId] => await HandleTrace(clientId),
    ["loglevel", var level] => await HandleLogLevel(level),
    [var cmd, ..] => LogError($"Unknown command: {cmd}"),
};

int LogError(string message)
{
    Console.WriteLine(message);
    return 1;
}

int ShowUsageAndReturn()
{
    var exeName = Process.GetCurrentProcess().ProcessName;

    Console.WriteLine("Machine Service CLI");
    Console.WriteLine("Usage:");
    Console.WriteLine($"{exeName} trace [clientid]      * Will disable all other logs and enable trace for the client");
    Console.WriteLine($"{exeName} trace off             * Will stop all traces");
    Console.WriteLine($"{exeName} trace reset           * Will stop all traces");
    Console.WriteLine($"{exeName} loglevel [loglevel]   * Will change the log level for the server");
    Console.WriteLine("                                   debug,information,warning,error,fatal");
    Console.WriteLine($"{exeName} loglevel reset        * Will reset the log level to default (configured when service started)");
    return 0;
}