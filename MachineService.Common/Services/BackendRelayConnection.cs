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
using MachineService.Common.Interfaces;
using MachineService.External;
using MassTransit;
using MassTransit.SqlTransport.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebSockets.Common;

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

/// <summary>
/// Handles relay messages from the backend server to the agent
/// </summary>
/// <param name="settings">The environment settings</param>
/// <param name="connectionListService">The connection list service</param>
/// <param name="pendingAgentControlService">The pending agent control service</param>
public class BackendRelayMessageHandler(EnvironmentConfig settings, ConnectionListService connectionListService, IPendingAgentControlService pendingAgentControlService, IStatisticsGatherer statisticsGatherer)
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
            try
            {
                // Prepare the message
                var message = new EnvelopedMessage()
                {
                    From = settings.MachineName,
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
                using var ct = new CancellationTokenSource(ControlResponseTimeout);
                var responseTask = pendingAgentControlService.PrepareForControlResponse(message.MessageId, client.ClientId, ct.Token);

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
                ));
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
                ));
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
        ));
    }
}

/// <summary>
/// Extension methods for registering MassTransit with the service collection
/// </summary>
public static class BackendRelayConnectionExtensions
{
    /// <summary>
    /// Registers MassTransit with the service collection, configuring it to use either
    /// in-memory transport or PostgreSQL transport based on the environment and configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="builder">The web application builder</param>
    /// <returns>The updated service collection</returns>
    public static IServiceCollection RegisterMassTransit(this IServiceCollection services, WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetValue<string>("Messaging:ConnectionString");
        return services.AddMassTransit(x =>
        {
            x.AddRequestClient<ValidateAgentRequestToken>();
            x.AddRequestClient<ValidateConnectRequestToken>();
            x.AddConsumer<BackendRelayMessageHandler>();
            if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(connectionString))
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new Exception("Messaging connection string is not set in configuration");
                x.UsingPostgres((ctx, configurator) =>
                {
                    configurator.Host(new PostgresSqlHostSettings(new SqlTransportOptions
                    {
                        ConnectionString = connectionString
                    }));

                    configurator.ConfigureEndpoints(ctx);
                });
            }
        });
    }
}

