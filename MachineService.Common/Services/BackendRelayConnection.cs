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
/// Provides a connection to the portal backend to validate both JWT and OAuth tokens.
///
/// In future the endpoints might be moved to a dedicated authentication service,
/// but for consumer of this class it will be all abstracted.
/// </summary>
/// <param name="config">Injected parameters that contain the URL for the backends</param>
public class BackendRelayConnection(IRequestClient<ValidateAgentRequestToken> requestAgentClient, IRequestClient<ValidateConnectRequestToken> requestConnectClient) : IBackendRelayConnection
{
    /// <summary>
    /// The time to wait for a request to complete
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Validates an agent JWT token by sending it to the console via MassTransit request/response.
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <returns>The validation result</returns>
    public async Task<AgentClientValidationResult> ValidateAgentToken(string token)
    {
        try
        {
            var resp = await requestAgentClient.GetResponse<TokenValidationResponse>(new ValidateAgentRequestToken(token), CancellationToken.None, RequestTimeout);
            if (resp == null)
                return AgentClientValidationResult.FailureResult(new TimeoutException("Request timed out"));

            if (resp.Message.Success)
                return AgentClientValidationResult.SuccessResult(
                    organizationId: resp.Message.OrganizationId!,
                    registeredAgentId: resp.Message.RegisteredAgentId!,
                    newToken: resp.Message.NewToken,
                    tokenExpiration: resp.Message.Expires ?? DateTimeOffset.MinValue
                );

            return AgentClientValidationResult.FailureResult(new Exception(resp.Message.Message));
        }
        catch (Exception ex)
        {
            return AgentClientValidationResult.FailureResult(ex);
        }
    }

    /// <summary>
    /// Validates an OAuth token by sending it to the console via MassTransit request/response.
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <returns>The validation result</returns>
    public async Task<OAuthValidationResult> ValidateOAuthToken(string token)
    {
        var resp = await requestConnectClient.GetResponse<TokenValidationResponse>(new ValidateConnectRequestToken(token), CancellationToken.None, RequestTimeout);
        if (resp == null)
            return OAuthValidationResult.FailureResult(new TimeoutException("Request timed out"));

        if (resp.Message.Success)
            return OAuthValidationResult.SuccessResult(
                organizationId: resp.Message.OrganizationId!,
                tokenExpiration: resp.Message.Expires!.Value);

        return OAuthValidationResult.FailureResult(new Exception(resp.Message.Message));
    }
}
