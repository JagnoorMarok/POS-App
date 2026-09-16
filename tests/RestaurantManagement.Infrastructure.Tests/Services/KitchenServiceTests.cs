using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

internal class FakeDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    public DateTime Now => UtcNow.ToLocalTime();
}

internal class FakeCurrentUserService : ICurrentUserService
{
    public Guid? EmployeeId { get; set; } = Guid.NewGuid();
    public string? Username { get; set; } = "chef_gordon";
    public string? DisplayName { get; set; } = "Gordon Ramsay";
    public EmployeeRole? Role { get; set; } = EmployeeRole.KitchenStaff;
    public bool IsAuthenticated { get; set; } = true;

    public bool IsInRole(EmployeeRole role) => Role == role;
    public bool CanAccess(string featureTag) => true;

    public event Action? CurrentUserChanged;

    public void SetUser(Guid employeeId, string username, string displayName, EmployeeRole role)
    {
        EmployeeId = employeeId;
        Username = username;
        DisplayName = displayName;
        Role = role;
        IsAuthenticated = true;
        CurrentUserChanged?.Invoke();
    }

    public void ClearUser()
    {
        EmployeeId = null;
        Username = null;
        DisplayName = null;
        Role = null;
        IsAuthenticated = false;
        CurrentUserChanged?.Invoke();
    }
}

public class KitchenServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly RestaurantDbContext _dbContext;
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly FakeCurrentUserService _currentUserService = new();
    private readonly IAuthorizationService _authorizationService = new AuthorizationService();
    private readonly KitchenService _kitchenService;

    private readonly DateTime _utcNow = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private readonly Category _defaultCategory;

    public KitchenServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantKitchenTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "kitchen_test.db");
        var connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(connectionString)
            .Options;

        _dbContext = new RestaurantDbContext(_dbContextOptions);
        _dbContext.Database.Migrate();

        _dateTimeProvider.UtcNow = _utcNow;

        _defaultCategory = new Category(Guid.NewGuid(), "Kitchen Test Category", "Description", 1, _utcNow);
        _dbContext.Categories.Add(_defaultCategory);
        _dbContext.SaveChanges();

        _kitchenService = new KitchenService(
            _dbContext,
            _dateTimeProvider,
            _currentUserService,
            _authorizationService,
            NullLogger<KitchenService>.Instance);
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
    public async Task GetKitchenOrdersAsync_ShouldReturnActiveKitchenOrders_InAscendingChronologicalOrder()
    {
        // Arrange
        var table = new RestaurantTable(Guid.NewGuid(), "T1", 4, _utcNow);
        var product = new Product(Guid.NewGuid(), _defaultCategory.Id, "Paneer Tikka", "Spicy", 250m, _utcNow);
        _dbContext.RestaurantTables.Add(table);
        _dbContext.Products.Add(product);

        var order1 = new Order(Guid.NewGuid(), "ORD-000001", OrderType.DineIn, _utcNow.AddMinutes(-20), table.Id);
        order1.AddItem(product, 2, notes: "Extra spicy");
        order1.TransitionTo(OrderStatus.Confirmed, _utcNow.AddMinutes(-19));
        order1.TransitionTo(OrderStatus.Preparing, _utcNow.AddMinutes(-15));

        var order2 = new Order(Guid.NewGuid(), "ORD-000002", OrderType.Takeaway, _utcNow.AddMinutes(-10));
        order2.AddItem(product, 1);
        order2.TransitionTo(OrderStatus.Confirmed, _utcNow.AddMinutes(-9));

        var order3 = new Order(Guid.NewGuid(), "ORD-000003", OrderType.Delivery, _utcNow.AddMinutes(-5));
        order3.AddItem(product, 3);
        order3.TransitionTo(OrderStatus.Confirmed, _utcNow.AddMinutes(-4));
        order3.TransitionTo(OrderStatus.Ready, _utcNow.AddMinutes(-2));

        _dbContext.Orders.AddRange(order1, order2, order3);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _kitchenService.GetKitchenOrdersAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].OrderNumber.Should().Be("ORD-000001");
        result[0].Status.Should().Be(OrderStatus.Preparing);
        result[0].Items.Should().HaveCount(1);
        result[0].Items[0].Notes.Should().Be("Extra spicy");

        result[1].OrderNumber.Should().Be("ORD-000002");
        result[1].Status.Should().Be(OrderStatus.Confirmed);

        result[2].OrderNumber.Should().Be("ORD-000003");
        result[2].Status.Should().Be(OrderStatus.Ready);
    }

    [Fact]
    public async Task GetKitchenOrdersAsync_ShouldExcludeDraftCompletedAndCancelledOrders()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _defaultCategory.Id, "Dal Makhani", "Creamy", 200m, _utcNow);
        _dbContext.Products.Add(product);

        var draftOrder = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _utcNow);
        draftOrder.AddItem(product, 1);

        var activeOrder = new Order(Guid.NewGuid(), "ORD-000002", OrderType.Takeaway, _utcNow);
        activeOrder.AddItem(product, 1);
        activeOrder.TransitionTo(OrderStatus.Confirmed, _utcNow);

        var completedOrder = new Order(Guid.NewGuid(), "ORD-000003", OrderType.Takeaway, _utcNow);
        completedOrder.AddItem(product, 1);
        completedOrder.TransitionTo(OrderStatus.Confirmed, _utcNow);
        completedOrder.TransitionTo(OrderStatus.Completed, _utcNow);

        var cancelledOrder = new Order(Guid.NewGuid(), "ORD-000004", OrderType.Takeaway, _utcNow);
        cancelledOrder.AddItem(product, 1);
        cancelledOrder.Cancel(_utcNow, "Customer changed mind");

        _dbContext.Orders.AddRange(draftOrder, activeOrder, completedOrder, cancelledOrder);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _kitchenService.GetKitchenOrdersAsync();

        // Assert
        result.Should().ContainSingle();
        result[0].OrderNumber.Should().Be("ORD-000002");
    }

    [Fact]
    public async Task StartPreparationAsync_ShouldTransitionStatusToPreparing()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _defaultCategory.Id, "Butter Chicken", "Rich gravy", 300m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 2, notes: "Less butter");
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var prepTime = _utcNow.AddMinutes(5);
        _dateTimeProvider.UtcNow = prepTime;

        // Act
        var result = await _kitchenService.StartPreparationAsync(order.Id);

        // Assert
        result.Status.Should().Be(OrderStatus.Preparing);
        result.UpdatedAt.Should().Be(prepTime);

        var saved = await _dbContext.Orders.FindAsync(order.Id);
        saved!.Status.Should().Be(OrderStatus.Preparing);
    }

    [Fact]
    public async Task StartPreparationAsync_ShouldThrowValidationException_WhenOrderIsNotConfirmed()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _defaultCategory.Id, "Butter Chicken", "Rich gravy", 300m, _utcNow);
        _dbContext.Products.Add(product);

        var draftOrder = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _utcNow);
        draftOrder.AddItem(product, 1);

        _dbContext.Orders.Add(draftOrder);
        await _dbContext.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _kitchenService.StartPreparationAsync(draftOrder.Id);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*must be in Confirmed status*");
    }

    [Fact]
    public async Task MarkOrderReadyAsync_ShouldTransitionStatusToReady()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _defaultCategory.Id, "Garlic Naan", "Crispy", 60m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 4);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        order.TransitionTo(OrderStatus.Preparing, _utcNow.AddMinutes(2));

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var readyTime = _utcNow.AddMinutes(12);
        _dateTimeProvider.UtcNow = readyTime;

        // Act
        var result = await _kitchenService.MarkOrderReadyAsync(order.Id);

        // Assert
        result.Status.Should().Be(OrderStatus.Ready);
        result.UpdatedAt.Should().Be(readyTime);

        var saved = await _dbContext.Orders.FindAsync(order.Id);
        saved!.Status.Should().Be(OrderStatus.Ready);
    }

    [Fact]
    public async Task MarkOrderServedAsync_ShouldTransitionStatusToServed()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _defaultCategory.Id, "Mango Lassi", "Chilled", 90m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 2);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        order.TransitionTo(OrderStatus.Ready, _utcNow.AddMinutes(5));

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _kitchenService.MarkOrderServedAsync(order.Id);

        // Assert
        result.Status.Should().Be(OrderStatus.Served);

        var saved = await _dbContext.Orders.FindAsync(order.Id);
        saved!.Status.Should().Be(OrderStatus.Served);
    }

    [Fact]
    public async Task KitchenOperations_ShouldPreserveFinancialIsolation_AndNotAlterPricesOrTotals()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _defaultCategory.Id, "Biryani", "Aromatic", 350m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.DineIn, _utcNow);
        order.AddItem(product, 2, discountAmount: 50m, taxAmount: 30m, notes: "Medium spicy");
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var expectedSubtotal = order.Subtotal;
        var expectedTotal = order.TotalAmount;
        var expectedDiscount = order.DiscountAmount;
        var expectedTax = order.TaxAmount;

        // Act - execute kitchen operations
        await _kitchenService.StartPreparationAsync(order.Id);
        await _kitchenService.MarkOrderReadyAsync(order.Id);
        await _kitchenService.MarkOrderServedAsync(order.Id);

        // Assert
        var reloaded = await _dbContext.Orders.Include(o => o.Items).FirstAsync(o => o.Id == order.Id);
        reloaded.Subtotal.Should().Be(expectedSubtotal);
        reloaded.TotalAmount.Should().Be(expectedTotal);
        reloaded.DiscountAmount.Should().Be(expectedDiscount);
        reloaded.TaxAmount.Should().Be(expectedTax);
        reloaded.Items.Should().HaveCount(1);
        reloaded.Items.First().UnitPrice.Should().Be(350m);
        reloaded.Items.First().Quantity.Should().Be(2);

        var reloadedProduct = await _dbContext.Products.FirstAsync(p => p.Id == product.Id);
        reloadedProduct.Price.Should().Be(350m);
    }

    [Fact]
    public async Task KitchenStatusChanges_ShouldNotReleaseTableOccupancy()
    {
        // Arrange
        var table = new RestaurantTable(Guid.NewGuid(), "T10", 4, _utcNow);
        var product = new Product(Guid.NewGuid(), _defaultCategory.Id, "Naan", "Hot", 40m, _utcNow);
        _dbContext.RestaurantTables.Add(table);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-000001", OrderType.DineIn, _utcNow, table.Id);
        order.AddItem(product, 2);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var orderService = new OrderService(_dbContext, _dateTimeProvider, NullLogger<OrderService>.Instance);

        // Check initial occupancy
        var initialMap = await orderService.GetTableOccupancyMapAsync();
        initialMap[table.Id].Should().BeTrue();

        // Act - advance kitchen states
        await _kitchenService.StartPreparationAsync(order.Id);
        var prepMap = await orderService.GetTableOccupancyMapAsync();
        prepMap[table.Id].Should().BeTrue("Table must remain occupied during preparing state.");

        await _kitchenService.MarkOrderReadyAsync(order.Id);
        var readyMap = await orderService.GetTableOccupancyMapAsync();
        readyMap[table.Id].Should().BeTrue("Table must remain occupied during ready state.");

        await _kitchenService.MarkOrderServedAsync(order.Id);
        var servedMap = await orderService.GetTableOccupancyMapAsync();
        servedMap[table.Id].Should().BeTrue("Table must remain occupied during served state until billing/completion.");
    }
}
