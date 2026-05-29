using BuildingBlocks.Domain;
using Xunit;

namespace Engagement.Domain.Tests.Seedwork;

public class ValueObjectTests
{
    private sealed class Money : ValueObject
    {
        public int Amount { get; }
        public string Currency { get; }
        public Money(int amount, string currency) { Amount = amount; Currency = currency; }
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    [Fact]
    public void Equal_when_all_components_match()
    {
        Assert.Equal(new Money(5, "USD"), new Money(5, "USD"));
        Assert.True(new Money(5, "USD") == new Money(5, "USD"));
    }

    [Fact]
    public void Not_equal_when_a_component_differs()
    {
        Assert.NotEqual(new Money(5, "USD"), new Money(5, "EUR"));
        Assert.True(new Money(5, "USD") != new Money(6, "USD"));
    }

    [Fact]
    public void Equal_values_share_hash_code()
    {
        Assert.Equal(new Money(5, "USD").GetHashCode(), new Money(5, "USD").GetHashCode());
    }
}
