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
using System.IO.Pipes;
using System.Text.Json;

namespace Interprocess.NamedPipes;

/// <summary>
/// Client implementation for sending messages through named pipes
/// </summary>
public class NamedPipeClient(string pipeName = "MachineServerPipe")
{
    /// <summary>
    /// Semaphore to ensure thread-safe access to the named pipe
    /// </summary>
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Sends a message to the server and waits for a response
    /// </summary>
    /// <param name="type">The type of message to send</param>
    /// <param name="data">Optional data to include with the message</param>
    /// <param name="cancelToken">Token to cancel the operation</param>
    /// <returns>Tuple containing success status and response message</returns>
    public async Task<(bool Success, IPCMessage? message)> SendMessageAsync(string type, string? data = null, CancellationToken cancelToken = default)
    {
        await _semaphore.WaitAsync(cancelToken);

        try
        {
            // Create and connect to the named pipe
            await using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipeClient.ConnectAsync(TimeSpan.FromSeconds(5), cancelToken);

            // Prepare and send the message using length-prefix framing
            var messageJson = JsonSerializer.Serialize(new IPCMessage { Type = type.ToString(), Data = data });
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);

            await using var writer = new BinaryWriter(pipeClient, System.Text.Encoding.UTF8, true);
            writer.Write(messageBytes.Length);
            writer.Write(messageBytes);

            // Read the response using length-prefix framing
            using var reader = new BinaryReader(pipeClient, System.Text.Encoding.UTF8, true);
            var responseLength = reader.ReadInt32();
            var responseBytes = reader.ReadBytes(responseLength);
            var response = JsonSerializer.Deserialize<IPCMessage>(responseBytes);

            return (true, response);
        }
        catch (Exception)
        {
            return (false, null);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}