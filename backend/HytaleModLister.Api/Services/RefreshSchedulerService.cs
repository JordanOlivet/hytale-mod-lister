using Cronos;

namespace HytaleModLister.Api.Services;

public class RefreshSchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RefreshSchedulerService> _logger;

    private CronExpression? _cronExpression;
    private DateTime? _nextRun;

    public DateTime? NextScheduledRefresh => _nextRun;

    public RefreshSchedulerService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<RefreshSchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Parse CRON expression
        var cronString = _configuration["Scheduler:RefreshCron"] ?? "0 0 * * *";
        try
        {
            _cronExpression = CronExpression.Parse(cronString);
            _logger.LogInformation("Scheduler configured with CRON: {Cron}", cronString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid CRON expression: {Cron}, scheduler disabled", cronString);
            return;
        }

        // Initial refresh on startup (force refresh to ignore cache)
        _logger.LogInformation("Running initial refresh on startup");
        await RunRefreshAsync(forceRefresh: true, stoppingToken);

        // Schedule loop
        while (!stoppingToken.IsCancellationRequested)
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(
                _configuration["Scheduler:Timezone"] ?? "UTC");
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);

            _nextRun = _cronExpression.GetNextOccurrence(now, timezone);

            if (_nextRun.HasValue)
            {
                var delay = _nextRun.Value - now;
                _logger.LogInformation("Next scheduled refresh at {Time} (in {Delay})",
                    _nextRun.Value, delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                    await RunRefreshAsync(forceRefresh: false, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            else
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task RunRefreshAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var refreshService = scope.ServiceProvider.GetRequiredService<IModRefreshService>();
            await refreshService.RefreshModsAsync(forceRefresh, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduled refresh");
        }
    }
}
