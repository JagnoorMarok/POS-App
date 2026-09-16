using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Reports.DTOs;
using RestaurantManagement.Application.Reports.Interfaces;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;

namespace RestaurantManagement.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        RestaurantDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        ILogger<ReportService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<SalesReportDto> GenerateSalesReportAsync(GenerateReportRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        var now = _dateTimeProvider.UtcNow;
        DateTime fromUtc;
        DateTime toUtc;
        string periodLabel;

        // Determine date range from preset or custom dates
        switch (request.PresetPeriod?.ToLowerInvariant())
        {
            case "yesterday":
                var yesterday = now.Date.AddDays(-1);
                fromUtc = yesterday;
                toUtc = yesterday.AddDays(1).AddTicks(-1);
                periodLabel = $"Yesterday ({yesterday:dd MMM yyyy})";
                break;
            case "last7days":
                fromUtc = now.Date.AddDays(-6);
                toUtc = now;
                periodLabel = "Last 7 Days";
                break;
            case "thismonth":
                fromUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                toUtc = now;
                periodLabel = $"This Month ({now:MMMM yyyy})";
                break;
            case "alltime":
                fromUtc = DateTime.MinValue;
                toUtc = DateTime.MaxValue;
                periodLabel = "All Time";
                break;
            case "today":
            default:
                if (request.FromUtc.HasValue && request.ToUtc.HasValue)
                {
                    fromUtc = request.FromUtc.Value;
                    toUtc = request.ToUtc.Value;
                    periodLabel = $"{fromUtc:dd MMM yyyy} - {toUtc:dd MMM yyyy}";
                }
                else
                {
                    fromUtc = now.Date;
                    toUtc = now.Date.AddDays(1).AddTicks(-1);
                    periodLabel = $"Today ({now:dd MMM yyyy})";
                }
                break;
        }

        // Fetch Orders in range
        var orders = await _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .AsNoTracking()
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc)
            .ToListAsync(cancellationToken);

        // Exclude cancelled orders from gross revenue
        var activeOrCompletedOrders = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        decimal grossSales = activeOrCompletedOrders.Sum(o => o.Subtotal);
        decimal totalDiscounts = activeOrCompletedOrders.Sum(o => o.DiscountAmount);
        decimal totalTaxes = activeOrCompletedOrders.Sum(o => o.TaxAmount);
        decimal netRevenue = activeOrCompletedOrders.Sum(o => o.TotalAmount);

        var allPayments = orders
            .SelectMany(o => o.Payments)
            .Where(p => p.Status == PaymentStatus.Completed)
            .ToList();

        decimal totalPaidAmount = allPayments.Sum(p => p.Amount);
        decimal totalOutstanding = Math.Max(0m, netRevenue - totalPaidAmount);

        int totalOrders = orders.Count;
        int completedOrdersCount = orders.Count(o => o.Status == OrderStatus.Completed);
        int cancelledOrdersCount = orders.Count(o => o.Status == OrderStatus.Cancelled);
        decimal averageOrderValue = activeOrCompletedOrders.Count > 0
            ? Math.Round(netRevenue / activeOrCompletedOrders.Count, 2)
            : 0m;

        // Payment Method Breakdown
        var paymentGroups = allPayments
            .GroupBy(p => p.PaymentMethod)
            .Select(g =>
            {
                var total = g.Sum(p => p.Amount);
                double pct = totalPaidAmount > 0 ? (double)(total / totalPaidAmount) * 100 : 0;
                return new PaymentMethodSalesDto(
                    g.Key,
                    g.Key.ToString(),
                    g.Count(),
                    total,
                    Math.Round(pct, 1));
            })
            .OrderByDescending(p => p.TotalAmount)
            .ToList();

        // Order Type Breakdown
        var orderTypeGroups = activeOrCompletedOrders
            .GroupBy(o => o.OrderType)
            .Select(g =>
            {
                var total = g.Sum(o => o.TotalAmount);
                double pct = netRevenue > 0 ? (double)(total / netRevenue) * 100 : 0;
                return new OrderTypeSalesDto(
                    g.Key,
                    g.Key.ToString(),
                    g.Count(),
                    total,
                    Math.Round(pct, 1));
            })
            .OrderByDescending(o => o.TotalAmount)
            .ToList();

        // Top Selling Products
        var topProducts = activeOrCompletedOrders
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ProductNameSnapshot)
            .Select(g =>
            {
                var qty = g.Sum(i => i.Quantity);
                var revenue = g.Sum(i => i.TotalAmount);
                var avgPrice = qty > 0 ? Math.Round(revenue / qty, 2) : 0m;
                return new TopSellingProductDto(g.Key, qty, revenue, avgPrice);
            })
            .OrderByDescending(p => p.QuantitySold)
            .Take(10)
            .ToList();

        // Daily Trend (for multi-day periods)
        var dailyTrend = activeOrCompletedOrders
            .GroupBy(o => o.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailySalesPointDto(
                g.Key,
                g.Key.ToString("dd MMM"),
                g.Sum(o => o.TotalAmount),
                g.Count()))
            .ToList();

        _logger.LogInformation("Generated sales report for period {Period}: {Orders} orders, Net: ₹{Net:N2} (User: {User})",
            periodLabel, totalOrders, netRevenue, _currentUserService.Username ?? "System");

        return new SalesReportDto(
            fromUtc,
            toUtc,
            periodLabel,
            grossSales,
            totalDiscounts,
            totalTaxes,
            netRevenue,
            totalPaidAmount,
            totalOutstanding,
            totalOrders,
            completedOrdersCount,
            cancelledOrdersCount,
            averageOrderValue,
            paymentGroups,
            orderTypeGroups,
            topProducts,
            dailyTrend);
    }

    private void EnsureAuthorized()
    {
        if (_currentUserService.IsAuthenticated)
        {
            var isAllowed = _authorizationService.CanAccessFeature(_currentUserService.Role, "reports");
            if (!isAllowed)
            {
                throw new UnauthorizedAccessException($"Role '{_currentUserService.Role}' is not authorized to view sales reports.");
            }
        }
    }
}
