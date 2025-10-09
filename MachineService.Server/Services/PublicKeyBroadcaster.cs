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
namespace MachineService.Server.Services;

/// <summary>
/// This is a background service that will broadcast the public key to the backend via massTransit
/// and once finished will exit.
/// </summary>
/// <param name="config">Injected configuration</param>
public class PublicKeyBroadcaster(EnvironmentConfig config, DerivedConfig derivedConfig, IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    /// <summary>
    /// Timeout in milliseconds to wait for publishing the public key
    /// </summary>
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromMilliseconds(5000);
    /// <summary>
    /// Timeout in milliseconds to wait before retrying to publish the public key
    /// if the previous attempt failed.
    /// </summary>
    private static readonly TimeSpan RetryTimeout = TimeSpan.FromMilliseconds(5000);

    /// <summary>
    /// Interval to republish the public key
    /// </summary>
    private static readonly TimeSpan RepublishInterval = TimeSpan.FromDays(2);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();

        IPublishPublicKeyMessage publishService = scope
            .ServiceProvider
            .GetRequiredService<IPublishPublicKeyMessage>();

        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshOnce(stoppingToken, publishService);
            await Task.Delay(RepublishInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Try to publish the public key once, retrying on failure until stoppingToken is cancelled.
    /// </summary>
    /// <param name="stoppingToken">The cancellation token to stop the operation</param>
    /// <param name="publishService">The publish service to use</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task RefreshOnce(CancellationToken stoppingToken, IPublishPublicKeyMessage publishService)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            try
            {
                linkedTokenSource.CancelAfter(PublishTimeout);

                await publishService.PublishMessage(
                    derivedConfig.PublicKeyHash,
                    derivedConfig.PrivateKey.ExportRSAPublicKeyPem(),
                    config.MachineName ?? throw new InvalidOperationException("Machine name not set"),
                    derivedConfig.Expires, linkedTokenSource.Token);
                // If successful, we can break out of the loop, public key was broadcasted
                Log.Information($"Public key {derivedConfig.PublicKeyHash} successfully published on MassTransit for propagation");
                break;
            }
            catch (TaskCanceledException timeout)
            {
                Log.Warning(timeout, "Timeout trying to publish public key on MassTransit");
            }
            catch (Exception e)
            {
                Log.Error(e, "Error in public key background service");
            }
            finally
            {
                linkedTokenSource.Cancel();
            }

            await Task.Delay(RetryTimeout, stoppingToken);
        }
    }
}