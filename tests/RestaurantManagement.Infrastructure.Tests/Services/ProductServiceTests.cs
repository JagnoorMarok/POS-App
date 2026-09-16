using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Menu.DTOs;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class ProductServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ProductServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantProductTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "product_test.db");
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

    private async Task<Category> SeedCategoryAsync(string name = "Main Course")
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var category = new Category(Guid.NewGuid(), name, "Category desc", 1, DateTime.UtcNow);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    [Fact]
    public async Task CreateProductAsync_ShouldCreateProduct_WhenValid()
    {
        var category = await SeedCategoryAsync("Burgers");

        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new ProductService(context, _dateTimeProvider, NullLogger<ProductService>.Instance);

        var request = new CreateProductRequest(
            Name: "Cheeseburger",
            Price: 12.99m,
            CategoryId: category.Id,
            Description: "Classic beef burger with cheddar",
            DisplayOrder: 1,
            IsActive: true,
            IsAvailable: true);

        var result = await service.CreateProductAsync(request);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Cheeseburger");
        result.Price.Should().Be(12.99m);
        result.CategoryId.Should().Be(category.Id);
        result.CategoryName.Should().Be("Burgers");
        result.IsAvailable.Should().BeTrue();
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateProductAsync_ShouldRejectNegativePrice()
    {
        var category = await SeedCategoryAsync();

        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new ProductService(context, _dateTimeProvider, NullLogger<ProductService>.Instance);

        var request = new CreateProductRequest(
            Name: "Free Burger",
            Price: -5.00m,
            CategoryId: category.Id);

        var act = async () => await service.CreateProductAsync(request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public async Task CreateProductAsync_ShouldRejectInvalidCategoryId()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new ProductService(context, _dateTimeProvider, NullLogger<ProductService>.Instance);

        var nonExistentCategoryId = Guid.NewGuid();
        var request = new CreateProductRequest("Ghost Item", 9.99m, nonExistentCategoryId);

        var act = async () => await service.CreateProductAsync(request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*does not exist*");
    }

    [Fact]
    public async Task SetProductAvailabilityAsync_ShouldToggleAvailability()
    {
        var category = await SeedCategoryAsync();

        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new ProductService(context, _dateTimeProvider, NullLogger<ProductService>.Instance);

        var product = await service.CreateProductAsync(new CreateProductRequest("Ribeye Steak", 32.00m, category.Id));

        await service.SetProductAvailabilityAsync(product.Id, false);

        var updated = await service.GetProductByIdAsync(product.Id);
        updated!.IsAvailable.Should().BeFalse();

        await service.SetProductAvailabilityAsync(product.Id, true);
        var availableAgain = await service.GetProductByIdAsync(product.Id);
        availableAgain!.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task HistoricalOrderItemPrice_MustNotChange_WhenProductIsUpdatedOrDeactivated()
    {
        var category = await SeedCategoryAsync("Pasta");

        Guid productId;
        Guid orderId;

        // 1. Create product at $14.50
        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var productService = new ProductService(context, _dateTimeProvider, NullLogger<ProductService>.Instance);
            var prod = await productService.CreateProductAsync(new CreateProductRequest("Fettuccine Alfredo", 14.50m, category.Id));
            productId = prod.Id;
        }

        // 2. Create historical Order & OrderItem at snapshot price $14.50
        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var productEntity = await context.Products.FindAsync(productId);
            var order = new Order(Guid.NewGuid(), "ORD-HIST-001", OrderType.DineIn, DateTime.UtcNow);

            order.AddItem(productEntity!, quantity: 2);
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;
        }

        // 3. Update Product price to $22.00 and deactivate product
        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var productService = new ProductService(context, _dateTimeProvider, NullLogger<ProductService>.Instance);
            await productService.UpdateProductAsync(new UpdateProductRequest(productId, "Fettuccine Alfredo Deluxe", 22.00m, category.Id));
            await productService.DeactivateProductAsync(productId);
        }

        // 4. Verify historical order line items still hold original $14.50 snapshot
        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var historicalOrder = await context.Orders
                .Include(o => o.Items)
                .FirstAsync(o => o.Id == orderId);

            historicalOrder.Items.Should().HaveCount(1);
            var historicalItem = historicalOrder.Items.First();

            historicalItem.ProductNameSnapshot.Should().Be("Fettuccine Alfredo");
            historicalItem.UnitPrice.Should().Be(14.50m);
            historicalItem.Quantity.Should().Be(2);
            historicalItem.TotalAmount.Should().Be(29.00m);
            historicalOrder.TotalAmount.Should().Be(29.00m);
        }
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldDeleteProductAndPreserveHistoricalOrderItems()
    {
        var category = await SeedCategoryAsync("Drinks");

        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new ProductService(context, _dateTimeProvider, NullLogger<ProductService>.Instance);

        var product = await service.CreateProductAsync(new CreateProductRequest("Lemon Soda", 50.00m, category.Id));
        await service.DeleteProductAsync(product.Id);

        var deleted = await service.GetProductByIdAsync(product.Id);
        deleted.Should().BeNull();
    }
}
