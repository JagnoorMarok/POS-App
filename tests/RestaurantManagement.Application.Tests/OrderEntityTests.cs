using FluentAssertions;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using Xunit;

namespace RestaurantManagement.Application.Tests;

public class OrderEntityTests
{
    private readonly DateTime _now = DateTime.UtcNow;

    [Fact]
    public void Constructor_ShouldInitializeDraftOrder_WithCorrectFields()
    {
        var order = new Order(
            Guid.NewGuid(),
            "ORD-000001",
            OrderType.DineIn,
            _now,
            Guid.NewGuid(),
            "Window seat");

        order.OrderNumber.Should().Be("ORD-000001");
        order.OrderType.Should().Be(OrderType.DineIn);
        order.Status.Should().Be(OrderStatus.Draft);
        order.Notes.Should().Be("Window seat");
        order.Subtotal.Should().Be(0m);
        order.TotalAmount.Should().Be(0m);
        order.Items.Should().BeEmpty();
    }

    [Fact]
    public void AddItem_ShouldCalculateTotalsCorrectly()
    {
        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _now);

        order.AddItem(Guid.NewGuid(), "Burger", 150m, 2);
        order.AddItem(Guid.NewGuid(), "Fries", 80m, 1);

        order.Items.Should().HaveCount(2);
        order.Subtotal.Should().Be(380m); // 150*2 + 80*1 = 380
        order.TotalAmount.Should().Be(380m);
    }

    [Fact]
    public void UpdateItemQuantity_ShouldRecalculateTotals()
    {
        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _now);
        var item = order.AddItem(Guid.NewGuid(), "Burger", 150m, 2);

        order.Subtotal.Should().Be(300m);

        order.UpdateItemQuantity(item.Id, 4);

        order.Subtotal.Should().Be(600m);
        order.TotalAmount.Should().Be(600m);
    }

    [Fact]
    public void RemoveItem_ShouldRecalculateTotals()
    {
        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _now);
        var item1 = order.AddItem(Guid.NewGuid(), "Burger", 150m, 2);
        var item2 = order.AddItem(Guid.NewGuid(), "Fries", 80m, 1);

        order.Subtotal.Should().Be(380m);

        order.RemoveItem(item1.Id);

        order.Items.Should().HaveCount(1);
        order.Subtotal.Should().Be(80m);
        order.TotalAmount.Should().Be(80m);
    }

    [Theory]
    [InlineData(OrderStatus.Draft, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Draft, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Served)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Completed)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Served, OrderStatus.Completed)]
    [InlineData(OrderStatus.Served, OrderStatus.Cancelled)]
    public void TransitionTo_ShouldSucceed_ForValidTransitions(OrderStatus initialStatus, OrderStatus targetStatus)
    {
        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _now);
        if (initialStatus == OrderStatus.Confirmed)
        {
            order.TransitionTo(OrderStatus.Confirmed, _now);
        }
        else if (initialStatus == OrderStatus.Served)
        {
            order.TransitionTo(OrderStatus.Confirmed, _now);
            order.TransitionTo(OrderStatus.Served, _now.AddMinutes(2));
        }

        order.TransitionTo(targetStatus, _now.AddMinutes(5));

        order.Status.Should().Be(targetStatus);
        if (targetStatus == OrderStatus.Completed)
        {
            order.CompletedAt.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData(OrderStatus.Completed, OrderStatus.Draft)]
    [InlineData(OrderStatus.Completed, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Draft)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]
    public void TransitionTo_ShouldThrowInvalidOperationException_ForInvalidTransitions(OrderStatus initialStatus, OrderStatus targetStatus)
    {
        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _now);
        order.TransitionTo(OrderStatus.Confirmed, _now);
        order.TransitionTo(initialStatus, _now.AddMinutes(5));

        var act = () => order.TransitionTo(targetStatus, _now.AddMinutes(10));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateOrderDetails_ShouldThrowArgumentException_WhenDineInHasNoTable()
    {
        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _now);

        var act = () => order.UpdateOrderDetails(OrderType.DineIn, null, "Notes", 0m, _now);

        act.Should().Throw<ArgumentException>().WithParameterName("restaurantTableId");
    }
}
