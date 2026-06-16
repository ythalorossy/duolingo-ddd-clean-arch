using BuildingBlocks.Mediator;
using Engagement.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Host;

// The repo's first BackgroundService. Pure mechanism: on a timer, ask the application to settle any
// league week that has ended but isn't settled yet. The "what is due" policy lives in
// SettleDueLeagueWeeks (Application); this type owns only timing, scope, and lifecycle.
public sealed class LeagueSettlementScheduler(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<LeagueSettlementScheduler> logger,
    IOptions<LeagueSettlementOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer takes the injected TimeProvider, so FakeTimeProvider drives it in tests.
        using var timer = new PeriodicTimer(options.Value.PollInterval, clock);
        try
        {
            // do/while = run an initial pass on startup (fast catch-up), then once per interval.
            do
            {
                try
                {
                    // A BackgroundService is a singleton; the mediator/DbContext are scoped — so
                    // open a fresh scope per tick.
                    using var scope = scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    await mediator.SendAsync(new SettleDueLeagueWeeks(), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One bad tick (e.g. a transient DB error, or the schema not yet applied) must
                    // not kill the service for the Host's lifetime — log and retry next interval.
                    logger.LogError(ex, "League settlement tick failed; retrying next interval.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down — exit cleanly.
        }
    }
}
