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
using MachineService.External;
using MachineService.GatewayServer.Consumers;
using MachineService.Server.Consumers;
using MassTransit;
using MassTransit.SqlTransport.PostgreSql;

namespace MachineService.Common.Services;

/// <summary>
/// Extension methods for registering MassTransit with the service collection
/// </summary>
public static class MassTransitExtensions
{
    /// <summary>
    /// Registers MassTransit with the service collection, configuring it to use either
    /// in-memory transport or PostgreSQL transport based on the environment and configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="builder">The web application builder</param>
    /// <param name="envConfig">The environment configuration</param>
    /// <returns>The updated service collection</returns>
    public static IServiceCollection RegisterMassTransit(this IServiceCollection services, WebApplicationBuilder builder, EnvironmentConfig envConfig)
    {
        var connectionString = builder.Configuration.GetValue<string>("Messaging:ConnectionString");
        return services.AddMassTransit(x =>
        {
            x.AddRequestClient<ValidateConnectRequestToken>();
            if (envConfig.GatewayMode)
            {
                // In gateway mode, the gateway handles agent control commands
                x.AddConsumer<AgentControlCommandRequestHandler>();
            }
            else
            {
                x.AddRequestClient<ValidateAgentRequestToken>();
                x.AddConsumer<CleanupMessageHandler>();

                // If the server is not in gateway mode and no gateway servers are configured,
                // it should handle backend relay messages directly
                if (string.IsNullOrWhiteSpace(envConfig.GatewayServers))
                    x.AddConsumer<BackendControlMessageHandler>();
            }
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

                    configurator.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(envConfig.InstanceId, false));
                });
            }
        });
    }
}

