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
using System.Security.Cryptography;
using System.Text;
using Interprocess.NamedPipes;
using MachineService.Common;
using MachineService.Common.Middleware;
using MachineService.Common.Services;
using MachineService.Server.Events;
using MachineService.Server.Model;
using MachineService.Server.Services;
using MachineService.Server.Utility;
using MachineService.State.Interfaces;
using MachineService.State;
using Microsoft.OpenApi.Models;
using Serilog.Core;
using Serilog.Events;
using MachineService.GatewayClient.Services;
using ConsoleCommon;

var builder = WebApplication.CreateBuilder(args);

// Support the untracked local environment variables file for development
if (builder.Environment.IsDevelopment())
{
    // Load into environment variables
    var localEnvironmentVariables = new ConfigurationBuilder()
           .AddJsonFile("local.environmentvariables.json", optional: true, reloadOnChange: false)
           .Build().AsEnumerable().ToList();

    foreach (var (key, value) in localEnvironmentVariables)
        Environment.SetEnvironmentVariable(key, value);
}

builder.Configuration.AddEnvironmentVariables();

if (string.Equals(args.FirstOrDefault(), "codegen", StringComparison.OrdinalIgnoreCase))
{
    await MartenExtensions.Codegen(args.Skip(1).ToArray());
    return;
}

var connectionString = builder.Configuration.GetValue<string>("Database:ConnectionString");
var adminConnectionString = builder.Configuration.GetValue<string>("Database:AdminConnectionString");
var initializedSchema = false;

// For dev setups, we assume the connection string has admin rights if no admin connection string is explicitly set
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(adminConnectionString))
    adminConnectionString = connectionString;

if (!string.IsNullOrWhiteSpace(adminConnectionString))
{
    initializedSchema = true;
    await MartenExtensions.InitializeSchemaAsync(adminConnectionString);

    if (string.Equals(args.FirstOrDefault(), "initonly", StringComparison.OrdinalIgnoreCase))
    {
        Log.Information("Initialized database schema and exiting as requested");
        Console.WriteLine("Initialized database schema and exiting as requested");
        return;
    }
}

var envConfig = builder.Configuration.GetRequiredSection("Environment").Get<EnvironmentConfig>()
    ?? throw new Exception("Environment configuration section is missing");

// Verify schema by default, unless we just initialized it
if (envConfig.VerifySchema == null)
    envConfig = envConfig with { VerifySchema = !initializedSchema };

// If the machine name is not set, we set it to the environment machine name
if (string.IsNullOrWhiteSpace(envConfig.MachineName))
    envConfig = envConfig with { MachineName = Environment.MachineName };

// If the instanceId is not set, we set it to the machine name plus a GUID
if (string.IsNullOrWhiteSpace(envConfig.InstanceId))
    envConfig = envConfig with { InstanceId = $"{(envConfig.GatewayMode ? "gateway" : "service")}-{envConfig.MachineName}-{Guid.NewGuid()}" };

// Non-production environment always if development mode is enabled
if (builder.Environment.IsDevelopment())
    envConfig = envConfig with { IsProd = false };

if (string.IsNullOrWhiteSpace(envConfig.MachineServerPrivate) || envConfig.MachineServerKeyExpires == default)
{
    if (!builder.Environment.IsDevelopment())
        throw new Exception($"{nameof(envConfig.MachineServerPrivate)} or {nameof(envConfig.MachineServerKeyExpires)} is not set in configuration");

    // For development, we generate a temporary key in the project folder
    var contentRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    var privatekeyPath = Path.Combine(contentRoot, "debug-privatekey.pem");
    string privateKey;
    if (File.Exists(privatekeyPath))
        privateKey = File.ReadAllText(privatekeyPath);
    else
    {
        privateKey = Base64PemReader.Write(RSA.Create().ExportRSAPrivateKeyPem());
        File.WriteAllText(privatekeyPath, privateKey);
    }

    envConfig = envConfig with
    {
        MachineServerPrivate = privateKey,
        MachineServerKeyExpires = DateTimeOffset.UtcNow.AddDays(90)
    };
}

var securityconfig = builder.Configuration.GetSection("Security").Get<SimpleSecurityOptions>();
builder.AddSimpleSecurityFilter(securityconfig, msg => Log.Warning(msg));

builder.Services.AddCors(options =>
{
    if (!string.IsNullOrWhiteSpace(builder.Configuration.GetValue<string>("CORS:AllowedOrigins")))
        options.AddPolicy(name: "cors", t =>
        {
            t.WithOrigins(builder.Configuration.GetValue<string>("CORS:AllowedOrigins")!.Split(';'))
                .AllowAnyMethod()
                .AllowCredentials()
                .AllowAnyHeader();
        });
});

// Register WebSocket connection manager first to ensure it stops before database
builder.Services.AddSingleton<WebSocketConnectionManager>()
    .AddHostedService(sp => sp.GetRequiredService<WebSocketConnectionManager>());

builder.Services.AddSingleton(envConfig)
    .AddSingleton(DerivedConfig.Create(Base64PemReader.Read(envConfig.MachineServerPrivate), envConfig.MachineServerKeyExpires!.Value))
    .AddScoped<IBackendRelayConnection, BackendRelayConnection>()
    .RegisterMassTransit(builder, envConfig)
    .AddSingleton<ConnectionListService>()
    .AddSingleton<GatewayConnectionList>()
    .AddSingleton<IStatisticsGatherer, StatisticsGatherer>()
    .AddSingleton<IPendingAgentControlService, PendingAgentControlService>()
    .AddSingleton(new ServerStatistics());

var blacklistConfig = builder.Configuration.GetSection("IPBlacklist").Get<IPBlacklistConfig>();
if (blacklistConfig is not null && blacklistConfig.IsValid())
    builder.Services
        .AddSingleton(blacklistConfig)
        .AddSingleton<IPRestrictionLoaderService>();

if (!envConfig.RequiresDatabase || (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(connectionString)))
{
    builder.Services
        .AddSingleton<IStateManagerService, InMemoryStateManagerService>()
        .AddTransient<IStatisticsPersistenceService, InMemoryStatisticsPersistenceService>();
}
else
{
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new Exception("Database connection string is not set in configuration");

    if (envConfig.DisableDatabaseStatistics)
        builder.Services.AddTransient<IStatisticsPersistenceService, InMemoryStatisticsPersistenceService>();
    else
        builder.Services.AddTransient<IStatisticsPersistenceService, StatisticsPersistenceService>();

    if (envConfig.InMemoryClientList)
        builder.Services.AddSingleton<IStateManagerService, InMemoryStateManagerService>();
    else
        builder.Services.AddTransient<IStateManagerService, StateManagerService>();

    builder.Services.RegisterMartenDB(connectionString, envConfig.VerifySchema ?? true, envConfig.PreCompiledDbClasses ?? false);
}

builder.Services.AddHostedService<GatherStatisticsPersistence>();


// Set up logging
var envLogLevel = builder.Configuration.GetValue<string>("Serilog:MinimumLevel:Default");
var defaultLogLevel = !string.IsNullOrWhiteSpace(envLogLevel)
        ? Enum.Parse<LogEventLevel>(envLogLevel)
        : LogEventLevel.Information;

// Handle we will use to switch the loglevel on the fly
var logLevelSwitch = new LoggingLevelSwitch(defaultLogLevel);

string? traceTarget = null;

var serilogConfig = builder.Configuration.GetSection("Serilog").Get<SerilogConfig>();
var extra = new LoggingExtras() { IsProd = envConfig.IsProd, MachineName = envConfig.MachineName, Hostname = envConfig.InstanceId };
builder.AddCommonLogging(serilogConfig, extra, config =>
{
    // Filter if a trace target has been set, use that target. Otherwise, log everything
    config.MinimumLevel.ControlledBy(logLevelSwitch);
    config.Filter.ByIncludingOnly(e => traceTarget == null || e.MessageTemplate.ToString().Contains(traceTarget) || e.MessageTemplate.ToString().Contains("[Mandatory]"));
    config.WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{HttpRequestId}] ({ClientIp}) {RequestMethod} {RequestPath} | UA: {UserAgent} | {Message:lj}{NewLine}{Properties:j}{NewLine}{Exception}");
});

if (envConfig.EnableTraceDebugging)
{
    builder.Services.AddHostedService(sp =>
    {
        return new NamedPipeServerService(sp.GetRequiredService<ILogger<NamedPipeServerService>>(), "machineservice"
            , (message, writer, cancellationToken) =>
            {
                try
                {
                    switch (message.Type)
                    {
                        case "trace":
                            if (message.Data == "reset")
                            {
                                Log.Information("Trace request to change by external process to client {Trace}", message.Data);

                                logLevelSwitch.MinimumLevel = defaultLogLevel;
                                traceTarget = null;
                                writer.SendResponse(new IPCMessage { Type = "response", Data = $"Trace reset to default {logLevelSwitch.MinimumLevel}" });
                            }
                            else if (!string.IsNullOrWhiteSpace(message.Data))
                            {
                                // We force to debug level when trace is enabled.
                                logLevelSwitch.MinimumLevel = LogEventLevel.Debug;

                                // And set the trace target
                                traceTarget = message.Data;

                                Log.Information($"Trace request to change by external process to client/keyword {message.Data}");
                                writer.SendResponse(new IPCMessage { Type = "response", Data = $"Trace enabled." });
                            }
                            break;
                        case "loglevel":
                            Log.Information("Loglevel request to change by external process to {LogLevel}", message.Data);
                            logLevelSwitch.MinimumLevel = message.Data switch
                            {
                                "debug" => LogEventLevel.Debug,
                                "information" => LogEventLevel.Information,
                                "warning" => LogEventLevel.Warning,
                                "error" => LogEventLevel.Error,
                                "fatal" => LogEventLevel.Fatal,
                                "reset" => defaultLogLevel,
                                _ => throw new Exception("Invalid log level")
                            };
                            writer.SendResponse(new IPCMessage { Type = "response", Data = $"Log level set to {logLevelSwitch.MinimumLevel}" });
                            break;
                        default:
                            throw new Exception($"Unknown message type {message.Type}");
                    }

                }
                catch (Exception e)
                {
                    Log.Warning($"Error processing message: {e.Message}");
                    try
                    {
                        writer.SendResponse(new IPCMessage { Type = "response", Data = e.Message });
                    }
                    catch
                    {
                        // Can be ignored at this point.
                    }
                }
            });
    });
}

if (envConfig.IsUsingGatewayFeatures)
{
    if (string.IsNullOrWhiteSpace(envConfig.GatewayPreSharedKey))
        throw new Exception("GatewayPreSharedKey must be set when running in gateway mode");

    if (!builder.Environment.IsDevelopment())
    {
        if (string.IsNullOrWhiteSpace(envConfig.LicenseKey))
            throw new Exception("LicenseKey must be set when using gateway features");

        var license = await LicenseChecker.ObtainLicenseAsync(envConfig.LicenseKey, CancellationToken.None);
        license.EnsureFeatures(ConsoleLicenseFeatures.GatewayMachineServer);
        if (license.IsInGracePeriod)
            Log.Warning("The provided license is expired and in the grace period. It will stop working on {Expiration}", license.ValidToWithGrace);

        Log.Information("Using gateway features as allowed by license until {Expiration}", license.ValidTo);
    }
}

Dictionary<string, Type> behaviorCommandMap;
if (envConfig.GatewayMode)
{
    behaviorCommandMap = MachineService.GatewayServer.Behaviours.GatewayBehaviorMap.Behaviors;

    builder.Services
        .AddTransient<IAfterDisconnectBehavior, GatewayAfterDisconnectBehavior>();
}
else
{
    if (!string.IsNullOrWhiteSpace(envConfig.GatewayServers))
        builder.Services.AddHostedService<GatewayConnectionKeeper>();

    behaviorCommandMap = MachineService.Server.ServerBehaviorMap.Behaviors;

    builder.Services
        .AddScoped<IPublishPublicKeyMessage, PublishPublicKeyService>()
        .AddHostedService<PublicKeyBroadcaster>()
        .AddTransient<IAfterDisconnectBehavior, AfterDisconnectBehavior>()
        .AddTransient<IAfterAuthenticatedClientBehavior, AfterAuthenticatedClientBehavior>()
        .AddTransient<IPublishAgentActivityService, PublishAgentActivityService>();
}

foreach (var behavior in behaviorCommandMap)
    builder.Services.AddTransient(behavior.Value);


if (!envConfig.IsProd)
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Duplicati Inc. MachineServer Message Types Documentation", Version = "v1" });
        c.DocumentFilter<ExportTypeFilter>();
    });
builder.Services.AddControllers();

// Configure shutdown timeout to allow graceful WebSocket closure
builder.Host.ConfigureHostOptions(opts =>
{
    opts.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.UseCommonLogging();

// Log shutdown events
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
    Log.Information("Application stopping - WebSocket manager will close connections"));
lifetime.ApplicationStopped.Register(() =>
    Log.Information("Application stopped - all resources cleaned up"));


app.UseSimpleSecurityFilter(securityconfig);

if (!envConfig.IsProd)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Duplicati");
    });
}

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(1),
});

app.UseHttpsRedirection()
    .UseCors("cors");

if (blacklistConfig is not null && blacklistConfig.IsValid())
    await app.UseIpRestrictionAndLoad();

var statusReport = new Timer(_ =>
{
    var connectionList = app.Services.GetRequiredService<ConnectionListService>().GetConnections();
    var serverStatistics = app.Services.GetRequiredService<ServerStatistics>();
    var gatewayCount = app.Services.GetRequiredService<GatewayConnectionList>().Where(x => true).Count();
    var socketStates = connectionList.ToList();

    // We collect here instead of "on connection" because this is a non-critical statistic
    // and we don't want to slow down the connection process.
    serverStatistics.RecordCurrentConnections(socketStates.Count);

    ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
    ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);

    Log.Information(@"[Mandatory]
           Is Production: {isProd}
             Instance Id: {InstanceId}
            Server build: {GitCommit}
                  Uptime: {Uptime}
             Thread Pool: {BusyWorkerThreads} (Workers) & {BusyCompletionPortThreads} (Completion) of Max: {MaxWorkerThreads} & {MaxCompletionPortThreads}
       Connected Clients: {ConnectedClients}
Record Connected Clients: {MaxConnections} 
       Total connections: {TotalConnections}
 Total Messages Received: {TotalMessagesReceived}
     Gateway Connections: {GatewayConnections} of {GatewayServers}
               Log Level: {LogLevel}
                 Tracing: {TraceTarget}",
        envConfig.IsProd,
        envConfig.InstanceId,
        envConfig.GitVersion,
        serverStatistics.UptimeVerbose,
        maxWorkerThreads - workerThreads,
        maxCompletionPortThreads - completionPortThreads,
        maxWorkerThreads,
        maxCompletionPortThreads,
        socketStates.Count,
        serverStatistics.MaxSimultaneousConnections,
        serverStatistics.TotalConnections,
        serverStatistics.TotalMessagesReceived,
        gatewayCount,
        (envConfig.GatewayServers ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Length,
        logLevelSwitch.MinimumLevel.ToString(),
        traceTarget ?? "No trace active");

    if (logLevelSwitch.MinimumLevel == LogEventLevel.Debug)
    {
        StringBuilder stringBuilder = new();
        foreach (var connection in socketStates)
            stringBuilder.AppendLine($"{connection.ClientId}\t{connection.Type}\t{connection.Authenticated}\t{connection.OrganizationId}\t{connection.HandshakeComplete}\t{connection.LastReceived.DateTime.ToShortTimeString()}\t{connection.LastSent}");

        Log.Debug("Connected Clients:\n{ConnectedClients}", stringBuilder.ToString());
    }
}, null, TimeSpan.Zero, TimeSpan.FromSeconds(envConfig.StatusReportIntervalSeconds));

app.ConfigureWebSocketServer(behaviorCommandMap);

try
{
    Log.Information($"Starting application. Version: {typeof(Program).Assembly.GetName().Version}");
    app.Run();
}
catch (Exception ex)
{
    Log.Error(ex, "Crashed while running application");
}
finally
{
    Log.Information("Terminating application");
    Log.CloseAndFlush();
}