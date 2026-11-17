using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Materializes elapsed pending holds as Expired, and closes out no-shows (approved bookings that ended without a
    /// release) the same way. Availability checks already ignore both, so a delayed tick cannot keep an item blocked.
    /// </summary>
    public sealed class ReservationExpiryWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly TimeProvider _clock;
        private readonly ILogger<ReservationExpiryWorker> _logger;

        public ReservationExpiryWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ReservationExpiryWorker> logger)
        {
            _scopes = scopes;
            _clock = clock;
            _logger = logger;
        }

        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private const int BatchSize = 100;
        private const int MaxBatchesPerTick = 10;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval, _clock);
            try
            {
                do
                {
                    try
                    {
                        var total = 0;
                        for (var batch = 0; batch < MaxBatchesPerTick; batch++)
                        {
                            await using var scope = _scopes.CreateAsyncScope();
                            var service = scope.ServiceProvider.GetRequiredService<IReservationService>();
                            var count = await service.ExpireDueAsync(BatchSize, stoppingToken);
                            total += count;
                            if (count < BatchSize)
                            {
                                break;
                            }
                        }

                        if (total > 0)
                        {
                            _logger.LogInformation("Expired {Count} equipment reservations (elapsed holds and no-shows).", total);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        // A temporary SQL Server outage must not stop the web host or future ticks.
                        _logger.LogError(exception, "Could not expire pending equipment reservations.");
                    }
                } while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal host shutdown.
            }
        }
    }
}
