using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Orders.DTOs;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class OrderServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public OrderServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantOrderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "order_test.db");
        _connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        _dateTimeProvider = new DateTimeProvider();

        using var context = new RestaurantDbContext(_dbContextOptions);
        context.Database.Migrate();
    }

    public void Dispose()
    {
        try
        {
            var directory = Path.GetDirectoryName(_testDbPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldCreateDineInOrder_WhenTableIsValid()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var table = new RestaurantTable(Guid.NewGuid(), "T1", 4, _dateTimeProvider.UtcNow);
        context.RestaurantTables.Add(table);
        await context.SaveChangesAsync();

        var service = new OrderService(context, _dateTimeProvider, NullLogger<OrderService>.Instance);

        // Act
        var result = await service.CreateOrderAsync(new CreateOrderRequest(OrderType.DineIn, table.Id, "VIP Guest"));

        // Assert
        result.Should().NotBeNull();
        result.OrderNumber.Should().StartWith("ORD-");
        result.OrderType.Should().Be(OrderType.DineIn);
        result.Status.Should().Be(OrderStatus.Draft);
        result.RestaurantTableId.Should().Be(table.Id);
        result.TableNumber.Should().Be("T1");
        result.Notes.Should().Be("VIP Guest");
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldThrowValidationException_WhenDineInTableIsOccupied()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var table = new RestaurantTable(Guid.NewGuid(), "T1", 4, _dateTimeProvider.UtcNow);
        context.RestaurantTables.Add(table);

        var existingOrder = new Order(Guid.NewGuid(), "ORD-000001", OrderType.DineIn, _dateTimeProvider.UtcNow, table.Id);
        context.Orders.Add(existingOrder);
        await context.SaveChangesAsync();

        var service = new OrderService(context, _dateTimeProvider, NullLogger<OrderService>.Instance);

        // Act
        var act = () => service.CreateOrderAsync(new CreateOrderRequest(OrderType.DineIn, table.Id));

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*already has an active order*");
    }

    [Fact]
    public async Task AddItemToOrderAsync_ShouldConsolidateQuantity_WhenSameProductAddedWithoutNotes()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var category = new Category(Guid.NewGuid(), "Mains", "Main dishes", 1, _dateTimeProvider.UtcNow);
        context.Categories.Add(category);

        var product = new Product(Guid.NewGuid(), category.Id, "Butter Chicken", "Rich gravy", 300m, _dateTimeProvider.UtcNow);
        context.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _dateTimeProvider.UtcNow);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new OrderService(context, _dateTimeProvider, NullLogger<OrderService>.Instance);

        // Act
        await service.AddItemToOrderAsync(order.Id, new AddOrderItemRequest(product.Id, 2));
        var updated = await service.AddItemToOrderAsync(order.Id, new AddOrderItemRequest(product.Id, 3));

        // Assert
        updated.Items.Should().HaveCount(1);
        updated.Items[0].Quantity.Should().Be(5);
        updated.Subtotal.Should().Be(1500m); // 300 * 5
        updated.TaxAmount.Should().Be(75m); // 5% tax
        updated.TotalAmount.Should().Be(1575m);
    }

    [Fact]
    public async Task AddItemToOrderAsync_ShouldPreserveHistoricalPriceSnapshot_WhenProductPriceChangesLater()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var category = new Category(Guid.NewGuid(), "Mains", "Main dishes", 1, _dateTimeProvider.UtcNow);
        context.Categories.Add(category);

        var product = new Product(Guid.NewGuid(), category.Id, "Paneer Butter Masala", "Cottage cheese curry", 250m, _dateTimeProvider.UtcNow);
        context.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _dateTimeProvider.UtcNow);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new OrderService(context, _dateTimeProvider, NullLogger<OrderService>.Instance);

        // Act - Add item at 250
        await service.AddItemToOrderAsync(order.Id, new AddOrderItemRequest(product.Id, 2));

        // Change product price in catalog to 320
        product.UpdatePrice(320m, _dateTimeProvider.UtcNow);
        await context.SaveChangesAsync();

        // Reload order
        var reloadedOrder = await service.GetOrderByIdAsync(order.Id);

        // Assert - Existing item should still have UnitPrice 250
        reloadedOrder.Should().NotBeNull();
        reloadedOrder!.Items[0].UnitPrice.Should().Be(250m);
        reloadedOrder.Subtotal.Should().Be(500m);
        reloadedOrder.TaxAmount.Should().Be(25m); // 5% tax
        reloadedOrder.TotalAmount.Should().Be(525m);
    }

    [Fact]
    public async Task TableOccupancyMap_ShouldReflectOccupancyState()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var table1 = new RestaurantTable(Guid.NewGuid(), "T1", 2, _dateTimeProvider.UtcNow);
        var table2 = new RestaurantTable(Guid.NewGuid(), "T2", 4, _dateTimeProvider.UtcNow);
        context.RestaurantTables.AddRange(table1, table2);

        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.DineIn, _dateTimeProvider.UtcNow, table1.Id);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new OrderService(context, _dateTimeProvider, NullLogger<OrderService>.Instance);

        // Act
        var occupancy = await service.GetTableOccupancyMapAsync();

        // Assert
        occupancy[table1.Id].Should().BeTrue();
        occupancy[table2.Id].Should().BeFalse();

        // Complete order and verify table is released
        await service.CompleteOrderAsync(order.Id);
        var occupancyAfter = await service.GetTableOccupancyMapAsync();
        occupancyAfter[table1.Id].Should().BeFalse();
    }
}
