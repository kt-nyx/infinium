using Infinium.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Lease-renewal failure is exceptional.
#pragma warning disable CA1873 // The critical log is emitted only on failure.

namespace Infinium.Coordinator;

public sealed class CoordinatorLeaseRenewalService(
    AuthoritativeStore store,
    CoordinatorAuthority authority,
    ILogger<CoordinatorLeaseRenewalService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    store.RenewCoordinatorAuthority(
                        authority.FencingEpoch,
                        DateTimeOffset.UtcNow,
                        TimeSpan.FromMinutes(5));
                }
                catch (Exception exception)
                {
                    logger.LogCritical(
                        exception,
                        "Coordinator lease renewal failed for fencing epoch {FencingEpoch}.",
                        authority.FencingEpoch);
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

#pragma warning restore CA1873
#pragma warning restore CA1848
