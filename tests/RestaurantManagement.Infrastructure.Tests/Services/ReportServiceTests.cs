using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Reports.DTOs;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class ReportServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly RestaurantDbContext _dbContext;
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly FakeCurrentUserService _currentUserService = new();
    private readonly IAuthorizationService _authorizationService = new AuthorizationService();
    private readonly ReportService _reportService;

    private readonly DateTime _utcNow = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private readonly Category _category;

    public ReportServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantReportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "report_test.db");
        var connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(connectionString)
            .Options;

        _dbContext = new RestaurantDbContext(_dbContextOptions);
        _dbContext.Database.Migrate();

        _dateTimeProvider.UtcNow = _utcNow;
        _currentUserService.SetUser(Guid.NewGuid(), "manager_bob", "Bob Manager", EmployeeRole.Manager);

        _category = new Category(Guid.NewGuid(), "Main Course", "Food", 1, _utcNow);
        _dbContext.Categories.Add(_category);
        _dbContext.SaveChanges();

        _reportService = new ReportService(
            _dbContext,
            _dateTimeProvider,
            _currentUserService,
            _authorizationService,
            NullLogger<ReportService>.Instance);
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
            // Ignore file cleanup lock
        }
    }

    [Fact]
    public async Task GenerateSalesReportAsync_ShouldAggregateSalesTotalsPaymentsAndTopDishes()
    {
        // Arrange: Create products & orders
        var prod1 = new Product(Guid.NewGuid(), _category.Id, "Biryani", "Rice", 300.00m, _utcNow);
        var prod2 = new Product(Guid.NewGuid(), _category.Id, "Garlic Naan", "Bread", 60.00m, _utcNow);
        _dbContext.Products.AddRange(prod1, prod2);

        // Order 1: 2x Biryani = 600, Paid by Cash
        var order1 = new Order(Guid.NewGuid(), "ORD-RPT-001", OrderType.DineIn, _utcNow);
        order1.AddItem(prod1, 2);
        order1.TransitionTo(OrderStatus.Confirmed, _utcNow);
        order1.TransitionTo(OrderStatus.Served, _utcNow);
        order1.TransitionTo(OrderStatus.Completed, _utcNow);

        var payment1 = new Payment(Guid.NewGuid(), order1.Id, 600.00m, PaymentMethod.Cash, _utcNow, PaymentStatus.Completed);
        payment1.MarkCompleted(_utcNow);

        // Order 2: 1x Biryani (300) + 2x Naan (120) = 420, Paid by UPI
        var order2 = new Order(Guid.NewGuid(), "ORD-RPT-002", OrderType.Takeaway, _utcNow);
        order2.AddItem(prod1, 1);
        order2.AddItem(prod2, 2);
        order2.TransitionTo(OrderStatus.Confirmed, _utcNow);
        order2.TransitionTo(OrderStatus.Completed, _utcNow);

        var payment2 = new Payment(Guid.NewGuid(), order2.Id, 420.00m, PaymentMethod.DigitalWallet, _utcNow, PaymentStatus.Completed, "UPI-REF");
        payment2.MarkCompleted(_utcNow);

        _dbContext.Orders.AddRange(order1, order2);
        _dbContext.Payments.AddRange(payment1, payment2);
        await _dbContext.SaveChangesAsync();

        // Act
        var report = await _reportService.GenerateSalesReportAsync(new GenerateReportRequest(PresetPeriod: "Today"));

        // Assert
        report.Should().NotBeNull();
        report.TotalOrders.Should().Be(2);
        report.CompletedOrdersCount.Should().Be(2);
        report.NetRevenue.Should().Be(1020.00m);
        report.TotalPaidAmount.Should().Be(1020.00m);
        report.TotalOutstanding.Should().Be(0.00m);

        // Payment Methods Breakdown
        report.PaymentMethods.Should().HaveCount(2);
        report.PaymentMethods.Should().Contain(p => p.Method == PaymentMethod.Cash && p.TotalAmount == 600.00m);
        report.PaymentMethods.Should().Contain(p => p.Method == PaymentMethod.DigitalWallet && p.TotalAmount == 420.00m);

        // Top Selling Products
        report.TopProducts.Should().HaveCount(2);
        report.TopProducts.First().ProductName.Should().Be("Biryani");
        report.TopProducts.First().QuantitySold.Should().Be(3);
        report.TopProducts.First().TotalRevenue.Should().Be(900.00m);
    }

    [Fact]
    public async Task GenerateSalesReportAsync_AsCashier_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        _currentUserService.SetUser(Guid.NewGuid(), "cashier_carol", "Carol Cashier", EmployeeRole.Cashier);

        // Act & Assert
        var act = async () => await _reportService.GenerateSalesReportAsync(new GenerateReportRequest());
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
