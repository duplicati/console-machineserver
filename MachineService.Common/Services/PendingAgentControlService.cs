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
using MachineService.Common.Interfaces;

namespace MachineService.Common.Services;

/// <summary>
/// Pairing service to bridge control requests and responses
/// </summary>
public class PendingAgentControlService : IPendingAgentControlService
{
    /// <summary>
    /// The current list of pending responses
    /// </summary>
    private readonly Dictionary<string, TaskCompletionSource<ControlResponseMessage>> _pendingResponses = new();
    /// <summary>
    /// The lock that guards access to the pending responses
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Get the key for a message
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="clientId">The client ID</param>
    /// <param name="messageId">The message ID</param>
    /// <returns>The key for the message</returns>
    private static string GetKey(string organizationId, string clientId, string messageId) => $"{organizationId}:{clientId}:{messageId}";

    /// <inheritdoc/>
    public Task<ControlResponseMessage> PrepareForControlResponse(string organizationId, string clientId, string messageId, CancellationToken ct)
    {
        var key = GetKey(organizationId, clientId, messageId);
        var tcs = new TaskCompletionSource<ControlResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        lock (_lock)
        {
            if (ct.IsCancellationRequested)
            {
                tcs.TrySetCanceled(ct);
                return tcs.Task;
            }

            _pendingResponses[key] = tcs;
        }

        var registration = ct.Register(() =>
        {
            tcs.TrySetCanceled(ct);
            lock (_lock)
                if (_pendingResponses.TryGetValue(key, out var existing) && ReferenceEquals(existing, tcs))
                    _pendingResponses.Remove(key);
        });

        tcs.Task.ContinueWith(_ => registration.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return tcs.Task;
    }

    /// <inheritdoc/>
    public void SetControlResponse(string organizationId, string clientId, string messageId, ControlResponseMessage response)
    {
        var key = GetKey(organizationId, clientId, messageId);
        lock (_lock)
        {
            if (_pendingResponses.TryGetValue(key, out var tcs))
            {
                tcs.SetResult(response);
                _pendingResponses.Remove(key);
            }
        }
    }
}
