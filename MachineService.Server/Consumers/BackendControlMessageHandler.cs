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
using MachineService.External;
using MassTransit;

namespace MachineService.Server.Consumers;

/// <summary>
/// Handles control messages from the backend server to the agent
/// </summary>
/// <param name="settings">The environment settings</param>
/// <param name="connectionListService">The connection list service</param>
/// <param name="pendingAgentControlService">The pending agent control service</param>
public class BackendControlMessageHandler(EnvironmentConfig settings, ConnectionListService connectionListService, IPendingAgentControlService pendingAgentControlService, IStatisticsGatherer statisticsGatherer)
    : IConsumer<AgentControlCommandRequest>
{
    /// <summary>
    /// The time to wait for a control response from the agent
    /// </summary>
    private static readonly TimeSpan ControlResponseTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Consumes a control command request from the backend and relays it to the appropriate agent.
    /// </summary>
    /// <param name="context">The consume context containing the message</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task Consume(ConsumeContext<AgentControlCommandRequest> context)
    {
        // Find the agent connection
        var client = connectionListService.FirstOrDefault(x => x.RegisteredAgentId == context.Message.AgentId);
        if (client is { Authenticated: true, OrganizationId: not null, ClientId: not null, Type: ConnectionType.Agent })
        {
            statisticsGatherer.Increment(StatisticsType.ControlRelayInitiated);
            using var cts = new CancellationTokenSource(ControlResponseTimeout);
            try
            {
                // Prepare the message
                var message = new EnvelopedMessage()
                {
                    From = settings.InstanceId,
                    To = client.ClientId,
                    Type = MessageTypes.Control.ToString().ToLowerInvariant(),
                    Payload = EnvelopedMessage.SerializePayload(new ControlRequestMessage(
                        Command: context.Message.Command,
                        Parameters: context.Message.Settings
                    )),
                    ErrorMessage = null,
                    MessageId = Guid.NewGuid().ToString()
                };

                // Prepare to wait for a response
                var responseTask = pendingAgentControlService.PrepareForControlResponse(client.OrganizationId, client.ClientId, message.MessageId, cts.Token);

                // Send the message
                await client.WriteMessage(message, WrappingType.Encrypt, client.ClientPublicKey);

                // Wait for the response
                var response = await responseTask;

                statisticsGatherer.Increment(StatisticsType.ControlRelaySuccess);

                // Relay the response back to the backend
                await context.RespondAsync(new AgentControlCommandResponse(
                    AgentId: context.Message.AgentId,
                    OrganizationId: context.Message.OrganizationId,
                    Settings: response.Output,
                    Success: response.Success,
                    Message: response.ErrorMessage
                ), ctx => { ctx.TimeToLive = TimeSpan.FromMinutes(1); });
            }
            catch (Exception ex)
            {
                statisticsGatherer.Increment(StatisticsType.ControlRelayFailure);
                await context.RespondAsync(new AgentControlCommandResponse(
                    AgentId: context.Message.AgentId,
                    OrganizationId: context.Message.OrganizationId,
                    Settings: null,
                    Success: false,
                    Message: $"Failed to send message to client: {ex.Message}"
                ), ctx => { ctx.TimeToLive = TimeSpan.FromMinutes(1); });
            }
            finally
            {
                cts.Cancel();
            }

            return;
        }

        statisticsGatherer.Increment(StatisticsType.ControlRelayDestinationNotAvailable);
        await context.RespondAsync(new AgentControlCommandResponse(
            AgentId: context.Message.AgentId,
            OrganizationId: context.Message.OrganizationId,
            Settings: null,
            Success: false,
            Message: "Client was not connected"
        ), ctx => { ctx.TimeToLive = TimeSpan.FromMinutes(1); });
    }
}

