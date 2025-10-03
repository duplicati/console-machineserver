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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Interprocess.NamedPipes;

/// <summary>
/// Server implementation that listens for and handles client messages
/// </summary>
public class NamedPipeServerService : IHostedService, IDisposable
{
    /// <summary>
    /// Name of the pipe to create
    /// </summary>
    private readonly string _pipeName;
    /// <summary>
    /// Cancellation token source to signal server shutdown
    /// </summary>
    private readonly CancellationTokenSource _cts = new();
    /// <summary>
    /// Logger for diagnostic information
    /// </summary>
    private readonly ILogger<NamedPipeServerService> _logger;
    /// <summary>
    /// Delegate to handle incoming messages
    /// </summary>
    private readonly Action<IPCMessage, ResponseWriter, CancellationToken> _messageHandler;
    /// <summary>
    /// The named pipe server instance
    /// </summary>
    private NamedPipeServerStream? _pipeServer;

    /// <summary>
    /// Initializes a new instance of the server
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="pipeName">Name of the pipe to create</param>
    /// <param name="messageHandler">Delegate to handle incoming messages</param>
    public NamedPipeServerService(
        ILogger<NamedPipeServerService> logger,
        string pipeName,
        Action<IPCMessage, ResponseWriter, CancellationToken> messageHandler)
    {
        _logger = logger;
        _pipeName = pipeName;
        _messageHandler = messageHandler;
    }

    /// <summary>
    /// Creates a new instance of the named pipe server
    /// </summary>
    private void CreatePipeServer()
    {
        _pipeServer = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
    }

    /// <summary>
    /// Starts the server when the application starts
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var combinedCancellationTokens = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

        CreatePipeServer();
        _ = ListenForClientsAsync(combinedCancellationTokens.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the server when the application stops
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Continuous loop that listens for client connections until cancelationToken is triggered
    /// </summary>
    private async Task ListenForClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Wait for a client to connect
                await _pipeServer!.WaitForConnectionAsync(cancellationToken);
                // Handle the client connection in a separate task
                _ = HandleClientConnectionAsync(_pipeServer, cancellationToken);
                // Create a new pipe instance for the next client
                CreatePipeServer();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in pipe server");
            }
        }
    }

    /// <summary>
    /// Handles an individual client connection
    /// </summary>
    private async Task HandleClientConnectionAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
    {
        try
        {
            // Set up readers and writers for the pipe
            using var reader = new BinaryReader(pipeServer, System.Text.Encoding.UTF8, true);
            await using var writer = new BinaryWriter(pipeServer, System.Text.Encoding.UTF8, true);
            using var responseWriter = new ResponseWriter(writer);

            // Read the incoming message using length-prefix framing
            var messageLength = reader.ReadInt32();
            var messageBytes = reader.ReadBytes(messageLength);
            var messageJson = System.Text.Encoding.UTF8.GetString(messageBytes);
            var message = JsonSerializer.Deserialize<IPCMessage>(messageJson) ?? throw new InvalidOperationException("Failed to deserialize IPC message");

            // Process the message using the provided handler
            _messageHandler(message, responseWriter, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client connection");
        }
        finally
        {
            await pipeServer.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _pipeServer?.Dispose();
    }
}