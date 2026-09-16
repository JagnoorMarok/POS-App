using FluentAssertions;
using RestaurantManagement.Domain.Common;
using Xunit;

namespace RestaurantManagement.Application.Tests;

public class TestEntity : Entity<Guid>
{
    public string Name { get; }

    public TestEntity(Guid id, string name) : base(id)
    {
        Name = name;
    }
}

public class TestMoney : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public TestMoney(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}

public class DomainCommonTests
{
    [Fact]
    public void EntitiesWithSameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Item A");
        var entity2 = new TestEntity(id, "Item B");

        (entity1 == entity2).Should().BeTrue();
        entity1.Equals(entity2).Should().BeTrue();
    }

    [Fact]
    public void EntitiesWithDifferentIds_ShouldNotBeEqual()
    {
        var entity1 = new TestEntity(Guid.NewGuid(), "Item A");
        var entity2 = new TestEntity(Guid.NewGuid(), "Item A");

        (entity1 == entity2).Should().BeFalse();
        entity1.Equals(entity2).Should().BeFalse();
    }

    [Fact]
    public void ValueObjectsWithSameComponents_ShouldBeEqual()
    {
        var money1 = new TestMoney(100.50m, "USD");
        var money2 = new TestMoney(100.50m, "USD");

        (money1 == money2).Should().BeTrue();
        money1.Equals(money2).Should().BeTrue();
        money1.GetHashCode().Should().Be(money2.GetHashCode());
    }

    [Fact]
    public void ValueObjectsWithDifferentComponents_ShouldNotBeEqual()
    {
        var money1 = new TestMoney(100.50m, "USD");
        var money2 = new TestMoney(100.50m, "EUR");

        (money1 == money2).Should().BeFalse();
        money1.Equals(money2).Should().BeFalse();
    }
}
