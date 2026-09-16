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

public class BillingService : IBillingService
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        RestaurantDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        ILogger<BillingService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<BillDto?> GetBillForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        var order = await _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
            return null;

        var profile = await _dbContext.RestaurantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return MapToBillDto(order, profile);
    }

    public async Task<IReadOnlyList<BillSummaryDto>> GetOrdersAwaitingBillingAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        var orders = await _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Payments)
            .Include(o => o.Items)
            .AsNoTracking()
            .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        var list = new List<BillSummaryDto>();
        foreach (var o in orders)
        {
            var paidAmount = o.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .Sum(p => p.Amount);

            var remainingAmount = Math.Max(0m, o.TotalAmount - paidAmount);
            var isFullyPaid = remainingAmount == 0m && o.TotalAmount > 0;

            list.Add(new BillSummaryDto(
                o.Id,
                o.OrderNumber,
                o.OrderType,
                o.Status,
                o.RestaurantTableId,
                o.RestaurantTable?.TableNumber,
                o.CreatedAt,
                o.TotalAmount,
                paidAmount,
                remainingAmount,
                isFullyPaid,
                o.Items.Count));
        }

        return list;
    }

    public async Task<BillDto> ApplyDiscountAsync(ApplyDiscountRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        if (request.DiscountAmount < 0)
        {
            throw new ValidationException(nameof(request.DiscountAmount), "Discount amount cannot be negative.");
        }

        var order = await _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), request.OrderId);
        }

        if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
        {
            throw new ValidationException($"Cannot modify discount on an order with status {order.Status}.");
        }

        if (request.DiscountAmount > order.Subtotal)
        {
            throw new ValidationException(nameof(request.DiscountAmount), $"Discount (₹{request.DiscountAmount:N2}) cannot exceed subtotal (₹{order.Subtotal:N2}).");
        }

        order.UpdateOrderDetails(
            order.OrderType,
            order.RestaurantTableId,
            order.Notes,
            request.DiscountAmount,
            _dateTimeProvider.UtcNow);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Applied discount of ₹{Discount:N2} to order {OrderNumber}", request.DiscountAmount, order.OrderNumber);

        var profile = await _dbContext.RestaurantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return MapToBillDto(order, profile);
    }

    private void EnsureAuthorized()
    {
        if (_currentUserService.IsAuthenticated)
        {
            var isAllowed = _authorizationService.CanAccessFeature(_currentUserService.Role, "billing");
            if (!isAllowed)
            {
                throw new UnauthorizedAccessException($"Role '{_currentUserService.Role}' is not authorized for billing operations.");
            }
        }
    }

    private static BillDto MapToBillDto(Order order, RestaurantProfile? profile)
    {
        var paidAmount = order.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        var remainingAmount = Math.Max(0m, order.TotalAmount - paidAmount);
        var isFullyPaid = remainingAmount == 0m && order.TotalAmount > 0;

        var items = order.Items.Select(i => new BillItemDto(
            i.Id,
            i.ProductId,
            i.ProductNameSnapshot,
            i.UnitPrice,
            i.Quantity,
            i.DiscountAmount,
            i.TaxAmount,
            i.TotalAmount,
            i.Notes)).ToList();

        var payments = order.Payments.Select(p => new PaymentDto(
            p.Id,
            p.OrderId,
            p.Amount,
            p.PaymentMethod,
            p.Status,
            p.TransactionReference,
            p.CreatedAt,
            p.CompletedAt)).OrderBy(p => p.CreatedAt).ToList();

        return new BillDto(
            order.Id,
            order.OrderNumber,
            order.OrderType,
            order.Status,
            order.RestaurantTableId,
            order.RestaurantTable?.TableNumber,
            profile?.RestaurantName ?? "Restaurant Management",
            profile?.Address,
            profile?.PhoneNumber,
            profile?.CurrencyCode ?? "INR",
            profile?.GstOrTaxNumber,
            order.Notes,
            order.CreatedAt,
            order.UpdatedAt,
            order.Subtotal,
            order.DiscountAmount,
            order.TaxAmount,
            order.TotalAmount,
            paidAmount,
            remainingAmount,
            isFullyPaid,
            items,
            payments);
    }
}
