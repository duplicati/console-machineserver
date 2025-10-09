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
using MachineService.State.Interfaces;
using MassTransit;
using Scheduler.Messages;

// Message definition from scheduler service
namespace Scheduler.Messages
{
    /// <summary>
    /// A simple message that is sent daily
    /// </summary>
    public record DailyMessage();
}

namespace MachineService.Server.Consumers
{
    /// <summary>
    /// Handles daily cleanup messages to purge stale data from the state manager and statistics persistence services.
    /// </summary>
    /// <param name="stateManagerService">The state manager service</param>
    /// <param name="statisticsPersistenceService">The statistics persistence service</param>
    public class CleanupMessageHandler(IStateManagerService stateManagerService, IStatisticsPersistenceService statisticsPersistenceService) : IConsumer<DailyMessage>
    {
        /// <inheritdoc />
        public async Task Consume(ConsumeContext<DailyMessage> context)
        {
            // Random delay to avoid multiple agents running cleanup at the same time
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(0, 30)));

            try
            {
                await stateManagerService.PurgeStaleData(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Log and continue
                Log.Error(ex, "Error during state cleanup");
            }

            try
            {
                await statisticsPersistenceService.PurgeStaleData(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Log and continue
                Log.Error(ex, "Error during statistics cleanup");
            }
        }
    }
}
