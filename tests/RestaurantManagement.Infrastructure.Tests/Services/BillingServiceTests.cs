using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Billing.DTOs;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class BillingServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly RestaurantDbContext _dbContext;
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly FakeCurrentUserService _currentUserService = new();
    private readonly IAuthorizationService _authorizationService = new AuthorizationService();
    private readonly BillingService _billingService;

    private readonly DateTime _utcNow = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private readonly Category _category;
    private readonly RestaurantProfile _profile;

    public BillingServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantBillingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "billing_test.db");
        var connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(connectionString)
            .Options;

        _dbContext = new RestaurantDbContext(_dbContextOptions);
        _dbContext.Database.Migrate();

        _dateTimeProvider.UtcNow = _utcNow;

        // Cashier user by default
        _currentUserService.SetUser(Guid.NewGuid(), "cashier_jane", "Jane Cashier", EmployeeRole.Cashier);

        _category = new Category(Guid.NewGuid(), "Mains", "Main dishes", 1, _utcNow);
        _profile = new RestaurantProfile(Guid.NewGuid(), "The Grand Bistro", _utcNow, "123 High St", "+91 99999 88888", "INR", "GSTIN12345");
        _dbContext.Categories.Add(_category);
        _dbContext.RestaurantProfiles.Add(_profile);
        _dbContext.SaveChanges();

        _billingService = new BillingService(
            _dbContext,
            _dateTimeProvider,
            _currentUserService,
            _authorizationService,
            NullLogger<BillingService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Ignore file lock cleanup in temp
        }
    }

    [Fact]
    public async Task GetBillForOrderAsync_ShouldReturnAccurateHistoricalBill_WithLineItemsAndBreakdown()
    {
        // Arrange
        var table = new RestaurantTable(Guid.NewGuid(), "T1", 4, _utcNow);
        var product1 = new Product(Guid.NewGuid(), _category.Id, "Paneer Butter Masala", "Curry", 240.00m, _utcNow);
        var product2 = new Product(Guid.NewGuid(), _category.Id, "Butter Naan", "Bread", 45.00m, _utcNow);
        _dbContext.RestaurantTables.Add(table);
        _dbContext.Products.AddRange(product1, product2);

        var order = new Order(Guid.NewGuid(), "ORD-BILL-001", OrderType.DineIn, _utcNow, table.Id);
        order.AddItem(product1, 2, discountAmount: 20m, taxAmount: 23m, notes: "Mild spicy");
        order.AddItem(product2, 4, discountAmount: 0m, taxAmount: 9m);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var bill = await _billingService.GetBillForOrderAsync(order.Id);

        // Assert
        bill.Should().NotBeNull();
        bill!.OrderId.Should().Be(order.Id);
        bill.OrderNumber.Should().Be("ORD-BILL-001");
        bill.RestaurantName.Should().Be("The Grand Bistro");
        bill.TableNumber.Should().Be("T1");
        bill.Items.Should().HaveCount(2);

        // Subtotal: 2 * 240 + 4 * 45 = 480 + 180 = 660
        bill.Subtotal.Should().Be(660.00m);
        bill.DiscountAmount.Should().Be(20.00m);
        bill.TaxAmount.Should().Be(32.00m);
        // Total: 660 - 20 + 32 = 672
        bill.TotalAmount.Should().Be(672.00m);
        bill.PaidAmount.Should().Be(0m);
        bill.RemainingAmount.Should().Be(672.00m);
        bill.IsFullyPaid.Should().BeFalse();
    }

    [Fact]
    public async Task GetBillForOrderAsync_ShouldPreserveHistoricalUnitPrice_WhenProductPriceChangesLater()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _category.Id, "Special Biryani", "Rice", 250.00m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-BILL-002", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 2);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Simulate Product Price update in menu catalog later
        product.UpdatePrice(350.00m, _utcNow.AddHours(2));
        await _dbContext.SaveChangesAsync();

        // Act
        var bill = await _billingService.GetBillForOrderAsync(order.Id);

        // Assert - Bill must reflect the historical snapshot (2 * 250 = 500), not the new price 350
        bill.Should().NotBeNull();
        bill!.Items.First().UnitPrice.Should().Be(250.00m);
        bill.Subtotal.Should().Be(500.00m);
        bill.TotalAmount.Should().Be(500.00m);
    }

    [Fact]
    public async Task GetOrdersAwaitingBillingAsync_ShouldExcludeCompletedAndCancelledOrders()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _category.Id, "Dal", "Lentils", 150m, _utcNow);
        _dbContext.Products.Add(product);

        var openOrder = new Order(Guid.NewGuid(), "ORD-OPEN", OrderType.Takeaway, _utcNow);
        openOrder.AddItem(product, 1);
        openOrder.TransitionTo(OrderStatus.Confirmed, _utcNow);

        var completedOrder = new Order(Guid.NewGuid(), "ORD-COMPLETED", OrderType.Takeaway, _utcNow);
        completedOrder.AddItem(product, 1);
        completedOrder.TransitionTo(OrderStatus.Confirmed, _utcNow);
        completedOrder.TransitionTo(OrderStatus.Completed, _utcNow);

        var cancelledOrder = new Order(Guid.NewGuid(), "ORD-CANCELLED", OrderType.Takeaway, _utcNow);
        cancelledOrder.AddItem(product, 1);
        cancelledOrder.Cancel(_utcNow);

        _dbContext.Orders.AddRange(openOrder, completedOrder, cancelledOrder);
        await _dbContext.SaveChangesAsync();

        // Act
        var awaiting = await _billingService.GetOrdersAwaitingBillingAsync();

        // Assert
        awaiting.Should().ContainSingle();
        awaiting[0].OrderNumber.Should().Be("ORD-OPEN");
    }

    [Fact]
    public async Task ApplyDiscountAsync_ShouldUpdateOrderDiscountAndRecalculateTotals()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _category.Id, "Thali", "Complete meal", 300m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-DISCOUNT", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 2); // Subtotal: 600
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var bill = await _billingService.ApplyDiscountAsync(new ApplyDiscountRequest(order.Id, 100m));

        // Assert
        bill.DiscountAmount.Should().Be(100m);
        bill.Subtotal.Should().Be(600m);
        bill.TotalAmount.Should().Be(500m);
        bill.RemainingAmount.Should().Be(500m);
    }

    [Fact]
    public async Task ApplyDiscountAsync_ShouldThrowValidationException_WhenDiscountExceedsSubtotal()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _category.Id, "Thali", "Complete meal", 200m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-DISCOUNT-ERR", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 1); // Subtotal: 200
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _billingService.ApplyDiscountAsync(new ApplyDiscountRequest(order.Id, 250m));

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*cannot exceed subtotal*");
    }

    [Fact]
    public async Task BillingOperations_ShouldThrowUnauthorizedAccessException_ForKitchenStaff()
    {
        // Arrange
        _currentUserService.SetUser(Guid.NewGuid(), "chef_mario", "Mario Chef", EmployeeRole.KitchenStaff);

        // Act
        Func<Task> act = async () => await _billingService.GetOrdersAwaitingBillingAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
