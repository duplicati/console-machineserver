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

using MachineService.Common;
using MachineService.Common.Enums;
using MachineService.Common.Exceptions;
using MachineService.Common.Services;
using MachineService.State.Interfaces;

namespace MachineService.Server.Behaviours;

/// <summary>
/// Behavior for handling list messages
/// </summary>
/// <param name="envConfig">The environment configuration</param>
/// <param name="derivedConfig">The derived configuration</param>
/// <param name="statisticsGatherer">The statistics gatherer service</param>
/// <param name="stateManagerService">The state manager service</param>
public class ListBehavior(
    EnvironmentConfig envConfig,
    DerivedConfig derivedConfig,
    IStatisticsGatherer statisticsGatherer,
    IStateManagerService stateManagerService) : IMessageBehavior
{
    /// <summary>
    /// The command this behavior handles
    /// </summary>
    public static string Command => MessageTypes.List.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketState state, EnvelopedMessage message)
    {
        Log.Debug("List request from {From}", message.From);

        if (state is { Authenticated: true, OrganizationId: not null })
        {
            if (state.ConnectionState != ConnectionState.ConnectedPortalAuthenticated)
                throw new PolicyViolationException(ErrorMessages.InvalidConnectionStateForList);

            var agentsForOrganization = await stateManagerService.GetAgents(state.OrganizationId);
            Log.Debug("Returning {Count} clients for organization {OrganizationId}", agentsForOrganization.Count, state.OrganizationId);

            await state.WriteMessage(new EnvelopedMessage
            {
                Type = message.Type,
                From = envConfig.InstanceId,
                MessageId = message.MessageId,
                To = message.From,
                Payload = EnvelopedMessage.SerializePayload(agentsForOrganization)
            }, derivedConfig);

            statisticsGatherer.Increment(StatisticsType.ListCommandSuccess);
        }
        else
        {
            Log.Warning($"Non authenticated, will not reply.");
        }
    }
}