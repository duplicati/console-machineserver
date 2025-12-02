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
/// The result of an OAuth validation
/// </summary>
/// <param name="Success">Whether the validation was successful</param>
/// <param name="Exception">The exception if not successful</param>
/// <param name="OrganizationId">The organization ID if successful</param>
/// <param name="TokenExpiration">The token expiration if successful</param>
/// <param name="Impersonated">Whether the token is an impersonation token</param>
public record OAuthValidationResult(
    bool Success,
    Exception? Exception,
    string? OrganizationId,
    DateTimeOffset? TokenExpiration,
    bool Impersonated)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="tokenExpiration">The token expiration date</param>
    /// <param name="impersonated">Whether the token is an impersonation token</param>
    /// <returns>The success result</returns>
    public static OAuthValidationResult SuccessResult(string organizationId, DateTimeOffset tokenExpiration, bool impersonated) =>
        new OAuthValidationResult(true, null, organizationId, tokenExpiration, impersonated);

    /// <summary>
    /// Creates a failure validation result.
    /// </summary>
    /// <param name="exception">The exception that caused the failure</param>
    /// <returns>The failure result</returns>
    public static OAuthValidationResult FailureResult(Exception exception) =>
        new OAuthValidationResult(false, exception, null, null, false);
}
