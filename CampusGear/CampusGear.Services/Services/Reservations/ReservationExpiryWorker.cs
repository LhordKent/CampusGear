using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CampusGear.Services.Reservations;

/// <summary>
/// Materializes elapsed pending holds as Expired. Availability checks already ignore
/// elapsed holds, so a delayed tick cannot keep an item blocked.
/// </summary>
public sealed class ReservationExpiryWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 100;
    private const int MaxBatchesPerTick = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        try
        {
            do
            {
                try
                {
                    var total = 0;
                    for (var batch = 0; batch < MaxBatchesPerTick; batch++)
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        var service = scope.ServiceProvider.GetRequiredService<IReservationService>();
                        var count = await service.ExpireDueAsync(BatchSize, stoppingToken);
                        total += count;
                        if (count < BatchSize)
                            break;
                    }

                    if (total > 0)
                        logger.LogInformation("Expired {Count} pending equipment reservations.", total);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    // A temporary SQL Server outage must not stop the web host or future ticks.
                    logger.LogError(exception, "Could not expire pending equipment reservations.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }
}
