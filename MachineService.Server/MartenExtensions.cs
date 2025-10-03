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
using System.Text.Json.Serialization.Metadata;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Commands;
using Marten;
using Oakton;
using Weasel.Core;

/// <summary>
/// Extension methods for setting up Marten in the service collection
/// </summary>
public static class MartenExtensions
{
    /// <summary>
    /// Configures MartenDB with the provided connection string.
    /// </summary>
    /// <param name="connectionString">The connection string for the MartenDB database</param>
    /// <param name="autoCreate">The auto-create behavior for the MartenDB schema</param>
    /// <returns>A configuration action for MartenDB</returns>
    private static Action<StoreOptions> Configure(string connectionString, AutoCreate autoCreate, TypeLoadMode mode) => options =>
    {
        options.Events.MetadataConfig.EnableAll();
        options.UseSystemTextJsonForSerialization(enumStorage: EnumStorage.AsString, configure: options =>
        {
            options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = {
                        static typeInfo =>
                        {
                            if (typeInfo.Kind != JsonTypeInfoKind.Object)
                                return;

                            foreach (JsonPropertyInfo propertyInfo in typeInfo.Properties)
                            {
                                propertyInfo.IsRequired = false;
                            }
                        }
                }
            };
        });

        options.DisableNpgsqlLogging = true;
        options.AutoCreateSchemaObjects = autoCreate;
        options.GeneratedCodeMode = mode;
        options.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;
        options.Connection(connectionString);

        StatisticsPersistenceService.ConfigureDatabase(options);
        StateManagerService.ConfigureDatabase(options);
    };

    /// <summary>
    /// Initializes the database schema for MartenDB using the provided connection string.
    /// </summary>
    /// <param name="connectionString">The connection string for the MartenDB database</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async Task InitializeSchemaAsync(string connectionString)
    {
        await using var store = DocumentStore.For(Configure(connectionString, AutoCreate.CreateOrUpdate, TypeLoadMode.Auto));
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    /// <summary>
    /// Registers MartenDB with the provided connection string and configures it for the application.
    /// </summary>
    /// <param name="services">The service collection to add MartenDB to</param>
    /// <param name="connectionString">The connection string for the MartenDB database</param>
    /// <param name="verifySchema">Whether to verify the database schema on startup</param>
    /// <param name="preCompiledDbClasses">Whether the database classes are pre-compiled</param>
    /// <returns>The updated service collection</returns>
    public static IServiceCollection RegisterMartenDB(this IServiceCollection services, string connectionString, bool verifySchema, bool preCompiledDbClasses)
    {
        var res = services.AddMarten(Configure(connectionString, AutoCreate.None, preCompiledDbClasses ? TypeLoadMode.Static : TypeLoadMode.Auto))
            .UseLightweightSessions();
        // Ideally, we should *always* verify the schema, but the reduced permissions in some environments
        // prevent the schema from being verified.
        if (verifySchema)
            res = res.AssertDatabaseMatchesConfigurationOnStartup();

        return res.Services;
    }

    /// <summary>
    /// Runs the Marten code generation commands
    /// </summary>
    /// <param name="actions">The code actions to perform</param>
    /// <returns>The exit code</returns>
    public static async Task Codegen(string[] actions)
    {
        if (actions.Length == 0)
            actions = [CodeAction.delete.ToString(), CodeAction.write.ToString(), CodeAction.test.ToString()];

        // Set up a mock connection string to pass validations
        var mockConnectionString = "Host=unused;Database=unused;Username=unused;Password=unused";

        foreach (var a in actions)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddMarten(Configure(mockConnectionString, AutoCreate.None, TypeLoadMode.Static));
            var code = await builder.Build().RunOaktonCommands(["codegen", a]);
            if (code != 0)
            {
                Console.WriteLine($"Code generation action '{a}' failed with exit code {code}");
                Environment.Exit(code);
            }
        }
    }
}