using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Inventory.DTOs;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class InventoryServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly RestaurantDbContext _dbContext;
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly FakeCurrentUserService _currentUserService = new();
    private readonly IAuthorizationService _authorizationService = new AuthorizationService();
    private readonly InventoryService _inventoryService;

    private readonly DateTime _utcNow = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private readonly Category _category;

    public InventoryServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantInventoryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "inventory_test.db");
        var connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(connectionString)
            .Options;

        _dbContext = new RestaurantDbContext(_dbContextOptions);
        _dbContext.Database.Migrate();

        _dateTimeProvider.UtcNow = _utcNow;
        _currentUserService.SetUser(Guid.NewGuid(), "manager_alice", "Alice Manager", EmployeeRole.Manager);

        _category = new Category(Guid.NewGuid(), "Beverages", "Drinks", 1, _utcNow);
        _dbContext.Categories.Add(_category);
        _dbContext.SaveChanges();

        _inventoryService = new InventoryService(
            _dbContext,
            _dateTimeProvider,
            _currentUserService,
            _authorizationService,
            NullLogger<InventoryService>.Instance);
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
            // Ignore file lock in temp
        }
    }

    [Fact]
    public async Task GetInventorySummaryAsync_ShouldReturnCatalogProductsWithCalculatedStockStatus()
    {
        // Arrange
        var product1 = new Product(Guid.NewGuid(), _category.Id, "Mango Lassi", "Drink", 90.00m, _utcNow);
        var product2 = new Product(Guid.NewGuid(), _category.Id, "Masala Chai", "Tea", 30.00m, _utcNow);
        _dbContext.Products.AddRange(product1, product2);
        await _dbContext.SaveChangesAsync();

        // Act
        var summary = await _inventoryService.GetInventorySummaryAsync();

        // Assert
        summary.Should().NotBeNull();
        summary.TotalItems.Should().Be(2);
        summary.Items.Should().HaveCount(2);
        summary.InStockCount.Should().Be(2);
    }

    [Fact]
    public async Task UpdateStockAsync_WhenStockSetToZero_ShouldMarkProductOutOfStockAndUnavailable()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _category.Id, "Fresh Lime Soda", "Drink", 60.00m, _utcNow);
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        // Act - Set stock to 0
        var result = await _inventoryService.UpdateStockAsync(new UpdateStockRequest(
            product.Id,
            NewStock: 0,
            Reason: "Stock Exhausted"));

        // Assert
        result.CurrentStock.Should().Be(0);
        result.StockStatus.Should().Be("Out of Stock");

        var dbProduct = await _dbContext.Products.FindAsync(product.Id);
        dbProduct!.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task AdjustStockAsync_RestockAndWasteDeltas_ShouldCorrectlyUpdateStockLevels()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _category.Id, "Cold Coffee", "Drink", 120.00m, _utcNow);
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        // Set initial stock to 20
        await _inventoryService.UpdateStockAsync(new UpdateStockRequest(product.Id, 20));

        // Act 1: Restock +15
        var afterRestock = await _inventoryService.AdjustStockAsync(new AdjustStockRequest(product.Id, 15, "New Batch"));
        afterRestock.CurrentStock.Should().Be(35);

        // Act 2: Waste -5
        var afterWaste = await _inventoryService.AdjustStockAsync(new AdjustStockRequest(product.Id, -5, "Damaged"));
        afterWaste.CurrentStock.Should().Be(30);
    }

    [Fact]
    public async Task UpdateStockAsync_NegativeStock_ShouldThrowValidationException()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _category.Id, "Juice", "Drink", 80.00m, _utcNow);
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _inventoryService.UpdateStockAsync(new UpdateStockRequest(product.Id, -10));
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetInventorySummaryAsync_AsKitchenStaff_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        _currentUserService.SetUser(Guid.NewGuid(), "chef_ramsay", "Ramsay Chef", EmployeeRole.KitchenStaff);

        // Act & Assert
        var act = async () => await _inventoryService.GetInventorySummaryAsync();
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
