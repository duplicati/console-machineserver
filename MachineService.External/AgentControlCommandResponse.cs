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
namespace MachineService.External;

/// <summary>
/// The response returned from the agent after executing a control command
/// </summary>
/// <remarks>
/// This message is sent via MassTransit, so be careful when changing it.
/// </remarks>
/// <param name="AgentId">The agent that was updated</param>
/// <param name="OrganizationId">The organization the agent belongs to</param>
/// <param name="Settings">The settings that were returned</param>
/// <param name="Success">Whether the update was successful</param>
/// <param name="Message">An optional message about the update</param>
public sealed record AgentControlCommandResponse(
    string AgentId,
    string OrganizationId,
    Dictionary<string, string?>? Settings,
    bool Success,
    string? Message
);
