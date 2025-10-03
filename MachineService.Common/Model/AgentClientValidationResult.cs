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
namespace MachineService.Common.Model;

/// <summary>
/// Represents the result of the agent JWT validation.
/// </summary>
/// <param name="Success">Indicates success</param>
/// <param name="Exception">Null if no error, if present, contains the inner exception</param>
/// <param name="OrganizationId">The verified organization ID</param>
/// <param name="NewToken">If Present, indicates the token will be replaced on the clinet</param>
/// <param name="RegisteredAgentId"></param>
/// <param name="TokenExpiration">Token expiration ddate</param>
public record AgentClientValidationResult(
    bool Success,
    Exception? Exception,
    string? OrganizationId,
    string? NewToken,
    string? RegisteredAgentId,
    DateTimeOffset? TokenExpiration)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="registeredAgentId">The registered agent ID</param>
    /// <param name="tokenExpiration">The token expiration date</param>
    /// <param name="newToken">The new token</param>
    /// <returns>The success result</returns>
    public static AgentClientValidationResult SuccessResult(string organizationId, string registeredAgentId, DateTimeOffset tokenExpiration, string? newToken = null) =>
        new AgentClientValidationResult(true, null, organizationId, newToken, registeredAgentId, tokenExpiration);

    /// <summary>
    /// Creates a failure validation result.
    /// </summary>
    /// <param name="exception">The exception that caused the failure</param>
    /// <returns>The failure result</returns>
    public static AgentClientValidationResult FailureResult(Exception exception) =>
        new AgentClientValidationResult(false, exception, null, null, null, null);
}
