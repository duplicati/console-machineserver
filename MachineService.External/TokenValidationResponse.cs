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
/// The result of validating a token
/// </summary>
/// <remarks>
/// This message is sent via MassTransit, so be careful when changing it.
/// </remarks>
/// <param name="Success">Whether the token is valid</param>
/// <param name="OrganizationId">The organization the token is valid for</param>
/// <param name="RegisteredAgentId">The agent registration id the token is valid for</param>
/// <param name="Expires">When the token expires</param>
/// <param name="NewToken">New token if current token is expiring soon</param>
/// <param name="Message">A message about the validation, if failed</param>
public sealed record TokenValidationResponse(bool Success, string? OrganizationId, string? RegisteredAgentId, DateTimeOffset? Expires, string? NewToken, string? Message = null);

