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

using MachineService.Common.Enums;
using MachineService.Common.Services;

namespace MachineService.Server.Behaviours;

/// <summary>
/// Handles response to control messages from agent to console
/// </summary>
/// <param name="pendingAgentControlService">The service for managing pending agent control messages</param>
/// <param name="statisticsGatherer">The service for gathering statistics</param>
public class ControlBehavior(IPendingAgentControlService pendingAgentControlService, IStatisticsGatherer statisticsGatherer) : IMessageBehavior
{
    /// <inheritdoc />
    public static string Command => MessageTypes.Control.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        if (state is { Authenticated: true, OrganizationId: not null, ClientId: not null, Type: ConnectionType.Agent })
        {
            Log.Debug("Got response for control message from {From}@{OrganizationId} to {To}", message.From, state.OrganizationId, message.To);

            var response = message.DeserializePayload<ControlResponseMessage>();
            pendingAgentControlService.SetControlResponse(message.MessageId!, state.ClientId, response!);
            statisticsGatherer.Increment(StatisticsType.ControlRelaySuccess);
        }
        else
        {
            Log.Warning($"Non authenticated agent, will not forward.");
            statisticsGatherer.Increment(StatisticsType.ControlRelayDestinationNotAuthenticated);
        }

        return Task.CompletedTask;
    }
}