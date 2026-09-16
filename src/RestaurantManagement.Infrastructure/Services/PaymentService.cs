using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Billing.DTOs;
using RestaurantManagement.Application.Billing.Interfaces;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;

namespace RestaurantManagement.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        RestaurantDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        ILogger<PaymentService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<PaymentDto> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        if (request.Amount <= 0)
        {
            throw new ValidationException(nameof(request.Amount), "Payment amount must be greater than zero.");
        }

        var order = await _dbContext.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), request.OrderId);
        }

        if (order.Status == OrderStatus.Completed)
        {
            throw new ValidationException($"Cannot record payment for an already completed order ({order.OrderNumber}).");
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new ValidationException($"Cannot record payment for a cancelled order ({order.OrderNumber}).");
        }

        var paidSoFar = order.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        var remaining = Math.Max(0m, order.TotalAmount - paidSoFar);

        if (request.Amount > remaining)
        {
            throw new ValidationException(nameof(request.Amount),
                $"Payment amount (₹{request.Amount:N2}) exceeds remaining balance (₹{remaining:N2}). Overpayment is not allowed.");
        }

        decimal? changeReturned = null;
        if (request.PaymentMethod == PaymentMethod.Cash && request.CashTendered.HasValue)
        {
            if (request.CashTendered.Value < request.Amount)
            {
                throw new ValidationException(nameof(request.CashTendered),
                    $"Cash tendered (₹{request.CashTendered.Value:N2}) cannot be less than payment amount (₹{request.Amount:N2}).");
            }
            changeReturned = request.CashTendered.Value - request.Amount;
        }

        var now = _dateTimeProvider.UtcNow;
        var payment = new Payment(
            Guid.NewGuid(),
            order.Id,
            request.Amount,
            request.PaymentMethod,
            now,
            PaymentStatus.Completed,
            request.TransactionReference);

        payment.MarkCompleted(now, request.TransactionReference);

        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recorded {Method} payment of ₹{Amount:N2} on order {OrderNumber} (Staff: {Staff})",
            request.PaymentMethod, request.Amount, order.OrderNumber, _currentUserService.Username ?? "System");

        return new PaymentDto(
            payment.Id,
            payment.OrderId,
            payment.Amount,
            payment.PaymentMethod,
            payment.Status,
            payment.TransactionReference,
            payment.CreatedAt,
            payment.CompletedAt,
            request.CashTendered,
            changeReturned);
    }

    public async Task<IReadOnlyList<PaymentDto>> GetPaymentsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        var payments = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return payments.Select(p => new PaymentDto(
            p.Id,
            p.OrderId,
            p.Amount,
            p.PaymentMethod,
            p.Status,
            p.TransactionReference,
            p.CreatedAt,
            p.CompletedAt)).ToList();
    }

    public async Task<PaymentSummaryDto> GetPaymentSummaryForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        var order = await _dbContext.Orders
            .Include(o => o.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        var payments = order.Payments
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PaymentDto(
                p.Id,
                p.OrderId,
                p.Amount,
                p.PaymentMethod,
                p.Status,
                p.TransactionReference,
                p.CreatedAt,
                p.CompletedAt))
            .ToList();

        var paid = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
        var remaining = Math.Max(0m, order.TotalAmount - paid);
        var isFullyPaid = remaining == 0m && order.TotalAmount > 0;

        return new PaymentSummaryDto(
            order.Id,
            order.OrderNumber,
            order.TotalAmount,
            paid,
            remaining,
            isFullyPaid,
            payments);
    }

    public async Task CompleteOrderSettlementAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        var order = await _dbContext.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        if (order.Status == OrderStatus.Completed)
        {
            return; // Already completed
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new ValidationException($"Cannot settle or complete a cancelled order ({order.OrderNumber}).");
        }

        var paidSoFar = order.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        var remaining = Math.Max(0m, order.TotalAmount - paidSoFar);

        if (remaining > 0)
        {
            throw new ValidationException(
                $"Cannot complete settlement for order {order.OrderNumber}. Outstanding balance of ₹{remaining:N2} remains unpaid.");
        }

        var now = _dateTimeProvider.UtcNow;
        order.TransitionTo(OrderStatus.Completed, now);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Order {OrderNumber} successfully settled and marked Completed (Staff: {Staff})",
            order.OrderNumber, _currentUserService.Username ?? "System");
    }

    private void EnsureAuthorized()
    {
        if (_currentUserService.IsAuthenticated)
        {
            var isAllowed = _authorizationService.CanAccessFeature(_currentUserService.Role, "billing");
            if (!isAllowed)
            {
                throw new UnauthorizedAccessException($"Role '{_currentUserService.Role}' is not authorized for payment operations.");
            }
        }
    }
}
