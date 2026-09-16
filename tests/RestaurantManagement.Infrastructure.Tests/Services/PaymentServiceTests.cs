using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Billing.DTOs;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Tables.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class PaymentServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly RestaurantDbContext _dbContext;
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly FakeCurrentUserService _currentUserService = new();
    private readonly IAuthorizationService _authorizationService = new AuthorizationService();
    private readonly PaymentService _paymentService;
    private readonly TableService _tableService;

    private readonly DateTime _utcNow = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private readonly Category _category;

    public PaymentServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantPaymentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "payment_test.db");
        var connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(connectionString)
            .Options;

        _dbContext = new RestaurantDbContext(_dbContextOptions);
        _dbContext.Database.Migrate();

        _dateTimeProvider.UtcNow = _utcNow;

        // Cashier user by default
        _currentUserService.SetUser(Guid.NewGuid(), "cashier_sam", "Sam Cashier", EmployeeRole.Cashier);

        _category = new Category(Guid.NewGuid(), "Mains", "Main dishes", 1, _utcNow);
        _dbContext.Categories.Add(_category);
        _dbContext.SaveChanges();

        _paymentService = new PaymentService(
            _dbContext,
            _dateTimeProvider,
            _currentUserService,
            _authorizationService,
            NullLogger<PaymentService>.Instance);

        _tableService = new TableService(
            _dbContext,
            _dateTimeProvider,
            NullLogger<TableService>.Instance);
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
    public async Task RecordPaymentAsync_CashWithTenderAndChange_ShouldRecordExactBillAmountAndReturnChange()
    {
        // Arrange: Bill total = 850, Cash Tendered = 1000, Expected Change = 150, Recorded Payment = 850
        var product = new Product(Guid.NewGuid(), _category.Id, "Combo Meal", "Full Meal", 850.00m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-CASH-001", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 1);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        order.TransitionTo(OrderStatus.Served, _utcNow);

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var request = new RecordPaymentRequest(
            OrderId: order.Id,
            Amount: 850.00m,
            PaymentMethod: PaymentMethod.Cash,
            TransactionReference: "Cash Receipt",
            CashTendered: 1000.00m);

        // Act
        var result = await _paymentService.RecordPaymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Amount.Should().Be(850.00m);
        result.CashTendered.Should().Be(1000.00m);
        result.ChangeReturned.Should().Be(150.00m);
        result.PaymentMethod.Should().Be(PaymentMethod.Cash);
        result.Status.Should().Be(PaymentStatus.Completed);

        // Verify summary
        var summary = await _paymentService.GetPaymentSummaryForOrderAsync(order.Id);
        summary.TotalAmount.Should().Be(850.00m);
        summary.PaidAmount.Should().Be(850.00m);
        summary.RemainingAmount.Should().Be(0.00m);
        summary.IsFullyPaid.Should().BeTrue();
    }

    [Fact]
    public async Task RecordPaymentAsync_PartialPayments_ShouldTrackRemainingBalanceAndAllowSettlement()
    {
        // Arrange: Bill total = 1000. Pay 600 Cash, then 400 UPI
        var product = new Product(Guid.NewGuid(), _category.Id, "Deluxe Thali", "Thali", 500.00m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-PART-002", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 2);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act 1: First payment of 600 Cash
        var payment1 = await _paymentService.RecordPaymentAsync(new RecordPaymentRequest(
            OrderId: order.Id,
            Amount: 600.00m,
            PaymentMethod: PaymentMethod.Cash,
            CashTendered: 600.00m));

        var summaryAfterP1 = await _paymentService.GetPaymentSummaryForOrderAsync(order.Id);
        summaryAfterP1.PaidAmount.Should().Be(600.00m);
        summaryAfterP1.RemainingAmount.Should().Be(400.00m);
        summaryAfterP1.IsFullyPaid.Should().BeFalse();

        // Act 2: Second payment of 400 UPI
        var payment2 = await _paymentService.RecordPaymentAsync(new RecordPaymentRequest(
            OrderId: order.Id,
            Amount: 400.00m,
            PaymentMethod: PaymentMethod.DigitalWallet,
            TransactionReference: "UPI-TXN-998877"));

        var summaryAfterP2 = await _paymentService.GetPaymentSummaryForOrderAsync(order.Id);
        summaryAfterP2.PaidAmount.Should().Be(1000.00m);
        summaryAfterP2.RemainingAmount.Should().Be(0.00m);
        summaryAfterP2.IsFullyPaid.Should().BeTrue();
        summaryAfterP2.Payments.Should().HaveCount(2);
    }

    [Fact]
    public async Task RecordPaymentAsync_Overpayment_ShouldBeRejected()
    {
        // Arrange: Bill = 800, Already paid = 500, Remaining = 300. Try paying 400
        var product = new Product(Guid.NewGuid(), _category.Id, "Item", "Desc", 800.00m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-OVER-003", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 1);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        await _paymentService.RecordPaymentAsync(new RecordPaymentRequest(
            OrderId: order.Id,
            Amount: 500.00m,
            PaymentMethod: PaymentMethod.Card));

        // Act & Assert
        var act = async () => await _paymentService.RecordPaymentAsync(new RecordPaymentRequest(
            OrderId: order.Id,
            Amount: 400.00m,
            PaymentMethod: PaymentMethod.Cash,
            CashTendered: 400.00m));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*exceeds remaining balance*");
    }

    [Fact]
    public async Task RecordPaymentAsync_CancelledOrder_ShouldBeRejected()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _category.Id, "Item", "Desc", 200.00m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-CANC-004", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 1);
        order.TransitionTo(OrderStatus.Cancelled, _utcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _paymentService.RecordPaymentAsync(new RecordPaymentRequest(
            OrderId: order.Id,
            Amount: 200.00m,
            PaymentMethod: PaymentMethod.Cash));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*cancelled*");
    }

    [Fact]
    public async Task RecordPaymentAsync_CompletedOrder_ShouldBeRejected()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _category.Id, "Item", "Desc", 200.00m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-COMP-005", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 1);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        order.TransitionTo(OrderStatus.Served, _utcNow);
        order.TransitionTo(OrderStatus.Completed, _utcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _paymentService.RecordPaymentAsync(new RecordPaymentRequest(
            OrderId: order.Id,
            Amount: 200.00m,
            PaymentMethod: PaymentMethod.Cash));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*completed*");
    }

    [Fact]
    public async Task CompleteOrderSettlementAsync_WhenFullyPaid_ShouldCompleteOrderAndReleaseTable()
    {
        // Arrange
        var table = new RestaurantTable(Guid.NewGuid(), "T-SETTLE-1", 4, _utcNow);
        _dbContext.RestaurantTables.Add(table);

        var product = new Product(Guid.NewGuid(), _category.Id, "Item", "Desc", 350.00m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-SETTLE-006", OrderType.DineIn, _utcNow, table.Id);
        order.AddItem(product, 1);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        order.TransitionTo(OrderStatus.Served, _utcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Verify table is occupied while order is active
        var tableDtoBefore = await _tableService.GetTableByIdAsync(table.Id);
        tableDtoBefore!.IsOccupied.Should().BeTrue();

        // Pay full amount
        await _paymentService.RecordPaymentAsync(new RecordPaymentRequest(
            OrderId: order.Id,
            Amount: 350.00m,
            PaymentMethod: PaymentMethod.CreditCard,
            TransactionReference: "CC-AUTH-112233"));

        // Verify table remains occupied before settlement is completed
        var tableDtoDuring = await _tableService.GetTableByIdAsync(table.Id);
        tableDtoDuring!.IsOccupied.Should().BeTrue();

        // Act - Complete settlement
        await _paymentService.CompleteOrderSettlementAsync(order.Id);

        // Assert
        var updatedOrder = await _dbContext.Orders.FindAsync(order.Id);
        updatedOrder!.Status.Should().Be(OrderStatus.Completed);

        // Verify table is now released/free
        var tableDtoAfter = await _tableService.GetTableByIdAsync(table.Id);
        tableDtoAfter!.IsOccupied.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteOrderSettlementAsync_WhenUnpaid_ShouldThrowValidationException()
    {
        // Arrange
        var product = new Product(Guid.NewGuid(), _category.Id, "Item", "Desc", 350.00m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-UNPAID-007", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 1);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _paymentService.CompleteOrderSettlementAsync(order.Id);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Outstanding balance*");
    }

    [Fact]
    public async Task RecordPaymentAsync_AsKitchenStaff_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        _currentUserService.SetUser(Guid.NewGuid(), "chef_gordon", "Gordon Chef", EmployeeRole.KitchenStaff);

        var product = new Product(Guid.NewGuid(), _category.Id, "Item", "Desc", 100.00m, _utcNow);
        _dbContext.Products.Add(product);

        var order = new Order(Guid.NewGuid(), "ORD-AUTH-008", OrderType.Takeaway, _utcNow);
        order.AddItem(product, 1);
        order.TransitionTo(OrderStatus.Confirmed, _utcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = async () => await _paymentService.RecordPaymentAsync(new RecordPaymentRequest(
            OrderId: order.Id,
            Amount: 100.00m,
            PaymentMethod: PaymentMethod.Cash));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
