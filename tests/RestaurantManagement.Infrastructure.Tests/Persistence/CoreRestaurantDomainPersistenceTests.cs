using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Persistence;

public class CoreRestaurantDomainPersistenceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;

    public CoreRestaurantDomainPersistenceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "restaurant_domain_test.db");
        _connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        // Apply all migrations up to date
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
            // Best-effort cleanup of isolated test directory
        }
    }

    [Fact]
    public async Task Category_ShouldCreateAndRetrieveSuccessfully()
    {
        var categoryId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var category = new Category(categoryId, "Main Course", "Delicious main dishes", 1, now);
            context.Categories.Add(category);
            await context.SaveChangesAsync();
        }

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var retrieved = await context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);
            retrieved.Should().NotBeNull();
            retrieved!.Name.Should().Be("Main Course");
            retrieved.Description.Should().Be("Delicious main dishes");
            retrieved.DisplayOrder.Should().Be(1);
            retrieved.IsActive.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Product_ShouldBelongToCategoryAndPersistCorrectly()
    {
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var category = new Category(categoryId, "Starters", "Appetizers", 1, now);
            var product = new Product(productId, categoryId, "Paneer Tikka", "Grilled cottage cheese", 220.50m, now, displayOrder: 1);

            context.Categories.Add(category);
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var retrievedProduct = await context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == productId);

            retrievedProduct.Should().NotBeNull();
            retrievedProduct!.Name.Should().Be("Paneer Tikka");
            retrievedProduct.Price.Should().Be(220.50m);
            retrievedProduct.Category.Should().NotBeNull();
            retrievedProduct.Category!.Name.Should().Be("Starters");
        }
    }

    [Fact]
    public async Task RestaurantTable_ShouldPersistAndEnforceUniqueTableNumber()
    {
        var tableId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var table = new RestaurantTable(tableId, "Table 12", 4, now, displayOrder: 12);
            context.RestaurantTables.Add(table);
            await context.SaveChangesAsync();
        }

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var retrieved = await context.RestaurantTables.FirstOrDefaultAsync(t => t.Id == tableId);
            retrieved.Should().NotBeNull();
            retrieved!.TableNumber.Should().Be("Table 12");
            retrieved.Capacity.Should().Be(4);
            retrieved.IsActive.Should().BeTrue();

            // Attempt duplicate table number
            var duplicateTable = new RestaurantTable(Guid.NewGuid(), "Table 12", 2, DateTime.UtcNow);
            context.RestaurantTables.Add(duplicateTable);
            var act = async () => await context.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task Order_WithOrderItems_ShouldCalculateTotalsAndPersist()
    {
        var categoryId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var category = new Category(categoryId, "Beverages", "Drinks", 1, now);
            var table = new RestaurantTable(tableId, "Table 5", 4, now);
            var lassi = new Product(productId1, categoryId, "Mango Lassi", "Yogurt drink", 90.00m, now);
            var chai = new Product(productId2, categoryId, "Masala Chai", "Hot spiced tea", 40.00m, now);

            context.Categories.Add(category);
            context.RestaurantTables.Add(table);
            context.Products.AddRange(lassi, chai);

            var order = new Order(orderId, "ORD-2026-0001", OrderType.DineIn, now, tableId);
            order.AddItem(lassi, 2, discountAmount: 10.00m, taxAmount: 8.50m); // (90*2) - 10 + 8.50 = 178.50
            order.AddItem(chai, 3, discountAmount: 0m, taxAmount: 6.00m);      // (40*3) - 0 + 6.00 = 126.00

            order.Subtotal.Should().Be(300.00m);       // 180 + 120
            order.DiscountAmount.Should().Be(10.00m);
            order.TaxAmount.Should().Be(14.50m);
            order.TotalAmount.Should().Be(304.50m);    // 300 - 10 + 14.50

            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var retrievedOrder = await context.Orders
                .Include(o => o.Items)
                .Include(o => o.RestaurantTable)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            retrievedOrder.Should().NotBeNull();
            retrievedOrder!.OrderNumber.Should().Be("ORD-2026-0001");
            retrievedOrder.RestaurantTable.Should().NotBeNull();
            retrievedOrder.RestaurantTable!.TableNumber.Should().Be("Table 5");
            retrievedOrder.Items.Should().HaveCount(2);
            retrievedOrder.TotalAmount.Should().Be(304.50m);
        }
    }

    [Fact]
    public async Task HistoricalPriceSnapshot_OrderItemPriceMustNotChangeWhenProductPriceChanges()
    {
        // 1. Arrange: Create Product with initial price of ₹250
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var category = new Category(categoryId, "Main Course", "Mains", 1, now);
            var butterChicken = new Product(productId, categoryId, "Butter Chicken", "Rich curry", 250.00m, now);

            context.Categories.Add(category);
            context.Products.Add(butterChicken);

            // Create order with 2 portions of Butter Chicken at ₹250
            var order = new Order(orderId, "ORD-HIST-001", OrderType.DineIn, now);
            order.AddItem(butterChicken, 2); // 2 * 250 = 500

            order.TotalAmount.Should().Be(500.00m);

            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        // 2. Act: 6 months later, product price increases to ₹300
        var futureDate = now.AddMonths(6);
        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var productToUpdate = await context.Products.FindAsync(productId);
            productToUpdate.Should().NotBeNull();
            productToUpdate!.UpdatePrice(300.00m, futureDate);
            await context.SaveChangesAsync();
        }

        // 3. Assert: Existing historical OrderItem must still retain ₹250.00 snapshot!
        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var currentProduct = await context.Products.FindAsync(productId);
            currentProduct.Should().NotBeNull();
            currentProduct!.Price.Should().Be(300.00m);

            var historicalOrder = await context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            historicalOrder.Should().NotBeNull();
            historicalOrder!.Items.Should().HaveCount(1);

            var item = historicalOrder.Items.First();
            item.ProductNameSnapshot.Should().Be("Butter Chicken");
            item.UnitPrice.Should().Be(250.00m);
            item.Quantity.Should().Be(2);
            item.TotalAmount.Should().Be(500.00m);
            historicalOrder.TotalAmount.Should().Be(500.00m);
        }
    }

    [Fact]
    public async Task Payment_ShouldPersistAndLinkToOrderCorrectly()
    {
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var order = new Order(orderId, "ORD-PAY-001", OrderType.Takeaway, now);
            context.Orders.Add(order);

            var payment = new Payment(paymentId, orderId, 450.00m, PaymentMethod.UPI, now, transactionReference: "UPI-TXN-12345");
            context.Payments.Add(payment);
            await context.SaveChangesAsync();
        }

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var retrievedPayment = await context.Payments
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            retrievedPayment.Should().NotBeNull();
            retrievedPayment!.Amount.Should().Be(450.00m);
            retrievedPayment.PaymentMethod.Should().Be(PaymentMethod.UPI);
            retrievedPayment.Status.Should().Be(PaymentStatus.Pending);
            retrievedPayment.TransactionReference.Should().Be("UPI-TXN-12345");
            retrievedPayment.Order.Should().NotBeNull();
            retrievedPayment.Order!.OrderNumber.Should().Be("ORD-PAY-001");

            // Mark completed
            retrievedPayment.MarkCompleted(DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var completedPayment = await context.Payments.FindAsync(paymentId);
            completedPayment.Should().NotBeNull();
            completedPayment!.Status.Should().Be(PaymentStatus.Completed);
            completedPayment.CompletedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task RestaurantProfile_ShouldPersistProfileSettings()
    {
        var profileId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var profile = new RestaurantProfile(
                profileId,
                "Royal Biryani House",
                now,
                "45 Heritage Way, Mumbai",
                "+91 98200 12345",
                "INR",
                "27AABCR1234M1Z5");

            context.RestaurantProfiles.Add(profile);
            await context.SaveChangesAsync();
        }

        await using (var context = new RestaurantDbContext(_dbContextOptions))
        {
            var retrieved = await context.RestaurantProfiles.FindAsync(profileId);
            retrieved.Should().NotBeNull();
            retrieved!.RestaurantName.Should().Be("Royal Biryani House");
            retrieved.CurrencyCode.Should().Be("INR");
            retrieved.GstOrTaxNumber.Should().Be("27AABCR1234M1Z5");
        }
    }
}
