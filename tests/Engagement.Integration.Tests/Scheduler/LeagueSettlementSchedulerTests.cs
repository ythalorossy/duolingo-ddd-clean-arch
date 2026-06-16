using BuildingBlocks.Mediator;
using Engagement.Application;
using Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Engagement.Integration.Tests.Scheduler;

public class LeagueSettlementSchedulerTests
{
    // Records every request sent; can throw on a chosen call index to simulate a failing tick.
    private sealed class SpyMediator(int throwOnCallIndex = -1) : IMediator
    {
        private readonly List<object> _sent = new();
        private readonly object _gate = new();
        private int _calls;
        public IReadOnlyList<object> Sent { get { lock (_gate) return _sent.ToList(); } }
        public int Attempts { get { lock (_gate) return _calls; } } // entries into SendAsync, incl. throws

        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            lock (_gate)
            {
                if (_calls++ == throwOnCallIndex)
                    throw new InvalidOperationException("simulated tick failure");
                _sent.Add(request);
            }
            return Task.FromResult((TResponse)(object)Unit.Value);
        }
        public Task PublishAsync(INotification notification, CancellationToken ct = default) => Task.CompletedTask;

        // Polls (the loop resumes on a background continuation after the fake clock advances).
        public async Task WaitForSendCountAsync(int count, TimeSpan timeout)
        {
            var start = Environment.TickCount64;
            while (Sent.Count < count)
            {
                if (Environment.TickCount64 - start > timeout.TotalMilliseconds)
                    throw new TimeoutException($"Expected {count} sends; saw {Sent.Count}.");
                await Task.Delay(10);
            }
        }

        public async Task WaitForAttemptsAsync(int count, TimeSpan timeout)
        {
            var start = Environment.TickCount64;
            while (Attempts < count)
            {
                if (Environment.TickCount64 - start > timeout.TotalMilliseconds)
                    throw new TimeoutException($"Expected {count} attempts; saw {Attempts}.");
                await Task.Delay(10);
            }
        }
    }

    private static IServiceScopeFactory ScopeFactoryFor(IMediator mediator)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => mediator);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static LeagueSettlementScheduler NewScheduler(IMediator mediator, FakeTimeProvider clock, TimeSpan interval) =>
        new(ScopeFactoryFor(mediator), clock, NullLogger<LeagueSettlementScheduler>.Instance,
            Options.Create(new LeagueSettlementOptions { PollInterval = interval }));

    private static FakeTimeProvider NewClock() => new(new DateTimeOffset(2030, 1, 21, 0, 0, 0, TimeSpan.Zero));
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task A_failing_tick_does_not_stop_the_loop()
    {
        var clock = NewClock();
        var spy = new SpyMediator(throwOnCallIndex: 0); // the startup tick throws
        var sut = NewScheduler(spy, clock, Interval);

        await sut.StartAsync(CancellationToken.None); // startup tick throws, is logged + swallowed
        await spy.WaitForAttemptsAsync(1, Timeout);   // the startup tick was attempted (and threw)
        await Task.Delay(100);                        // let the loop catch the throw and re-park on the timer
        clock.Advance(Interval);
        await spy.WaitForSendCountAsync(1, Timeout);  // a later tick still succeeds → loop survived
        await sut.StopAsync(CancellationToken.None);

        Assert.Single(spy.Sent);
    }
}
