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
using MachineService.External;
using MassTransit;

namespace MachineService.Common.Services;

/// <summary>
/// Service for publishing the agent activity to the bus
/// </summary>
/// <param name="busControl">Reference to the bus</param>
public class PublishAgentActivityService(IBusControl busControl) : IPublishAgentActivityService
{
    /// <summary>
    /// Publishes the public key message
    /// </summary>
    /// <param name="message">Message record</param>
    /// <param name="cancelationToken">CancellationToken</param>
    /// <returns></returns>
    public Task Publish(AgentActivityMessage message, CancellationToken cancelationToken)
        => busControl.Publish(message, cancelationToken);
}