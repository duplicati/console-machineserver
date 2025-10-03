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
using KVPSButter;

namespace MachineService.Server.Middleware;

/// <summary>
/// Middleware that restricts access based on IP rules stored in a PostgreSQL database.
/// Supports both direct IP matching and CIDR notation.
/// </summary>
public class IPRestrictionMiddleware(RequestDelegate next, ILogger<IPRestrictionMiddleware> logger, IPRestrictionLoaderService loaderService)
{
    /// <summary>
    /// Processes an HTTP request, blocking it if the remote IP matches any restriction rules.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task representing the completion of request processing.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Connection.RemoteIpAddress is { } address)
        {
            var rules = loaderService.GetRules();
            if (rules.Any(rule => rule.IsMatch(address)))
            {
                logger.LogWarning("Blocked request from IP {IP} with path {Path} and user agent {UserAgent} by IP rule",
                    address,
                    context.Request.Path,
                    context.Request.Headers.UserAgent.ToString());

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await next(context);
    }
}

/// <summary>
/// Service responsible for loading and caching IP restriction rules from a KVPSButter storage.
/// </summary>
public class IPRestrictionLoaderService
{
    /// <summary>
    /// The duration for which the rules are cached before reloading.
    /// </summary>
    private readonly TimeSpan _cacheTimeout;
    /// <summary>
    /// The cached list of IP rules.
    /// </summary>
    private List<IPRule> _rules = [];
    /// <summary>
    /// The last time the rules were loaded, in ticks.
    /// </summary>
    private long _lastLoadedTicks = -1;
    /// <summary>
    /// Configuration for the IP blacklist storage and entry.
    /// </summary>
    private readonly IPBlacklistConfig _blacklistConfig;
    /// <summary>
    /// Lock object for synchronizing rule reloads.
    /// </summary>
    private readonly object _reloadLock = new();
    /// <summary>
    /// Logger for logging information and errors.
    /// </summary>
    private readonly ILogger<IPRestrictionLoaderService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IPRestrictionLoaderService"/> class.
    /// </summary>
    /// <param name="blacklistConfig">The IP blacklist configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public IPRestrictionLoaderService(IPBlacklistConfig blacklistConfig, ILogger<IPRestrictionLoaderService> logger)
    {
        _blacklistConfig = blacklistConfig;
        _cacheTimeout = TimeSpan.FromSeconds(_blacklistConfig.IPRulesCacheLifetimeSeconds);
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_blacklistConfig.Storage) || string.IsNullOrWhiteSpace(_blacklistConfig.Entry))
            _lastLoadedTicks = long.MaxValue;
    }

    /// <summary>
    /// Reloads the IP restriction rules from the KVPSButter storage.
    /// </summary>
    /// <param name="rethrow">Whether to rethrow exceptions encountered during loading.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async Task ReloadRulesAsync(bool rethrow, CancellationToken cancellationToken)
    {
        lock (_reloadLock)
        {
            if (DateTime.UtcNow.Ticks - _lastLoadedTicks < TimeSpan.FromMinutes(1).Ticks)
                return;

            Interlocked.Exchange(ref _lastLoadedTicks, DateTime.UtcNow.Ticks);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(_blacklistConfig.Storage) || string.IsNullOrWhiteSpace(_blacklistConfig.Entry))
                return;

            using var conn = KVPSLoader.CreateIKVPS(_blacklistConfig.Storage);
            var rules = (await conn.ReadJsonAsync<List<IPRule>>(_blacklistConfig.Entry, cancellationToken)
                ?? throw new InvalidOperationException("Failed to load IP restriction rules"))
                .Where(r => r.IsValid())
                .ToList();

            Interlocked.Exchange(ref _rules, rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading IP restriction rules");
            if (rethrow)
                throw;
        }
    }

    /// <summary>
    /// Gets the current list of IP rules, refreshing from database if cache has expired.
    /// </summary>
    /// <returns>List of IP rules as strings.</returns>
    public List<IPRule> GetRules()
    {
        if (DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastLoadedTicks) > _cacheTimeout.Ticks)
            Task.Run(() => ReloadRulesAsync(false, CancellationToken.None));

        return _rules;
    }
}

/// <summary>
/// Extension methods for adding the IPRestrictionMiddleware to the ASP.NET Core pipeline.
/// </summary>
public static class IpRestrictionMiddlewareMiddlewareExtensions
{
    /// <summary>
    /// Adds the IPRestrictionMiddleware to the application's request pipeline and loads the initial rules.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder with the middleware added.</returns>
    public static async Task<IApplicationBuilder> UseIpRestrictionAndLoad(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<IPRestrictionMiddleware>();
        await builder.ApplicationServices.GetRequiredService<IPRestrictionLoaderService>()
            .ReloadRulesAsync(true, CancellationToken.None);
        return builder;
    }
}