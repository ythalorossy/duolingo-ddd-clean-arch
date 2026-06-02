using BuildingBlocks.Domain;
using Xunit;

namespace Engagement.Domain.Tests.Seedwork;

public class AggregateRootTests
{
    private sealed record SomethingHappened(DateTimeOffset OccurredOn) : IDomainEvent;

    private sealed class TestAggregate : AggregateRoot
    {
        public void DoSomething() => RaiseDomainEvent(new SomethingHappened(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Raised_events_are_exposed()
    {
        var agg = new TestAggregate();
        agg.DoSomething();
        Assert.Single(agg.DomainEvents);
        Assert.IsType<SomethingHappened>(agg.DomainEvents.First());
    }

    [Fact]
    public void Clear_empties_the_event_list()
    {
        var agg = new TestAggregate();
        agg.DoSomething();
        agg.ClearDomainEvents();
        Assert.Empty(agg.DomainEvents);
    }
}
