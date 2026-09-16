using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Reports.DTOs;

/// <summary>
/// Comprehensive sales, revenue, and order analytics report for a specified period.
/// </summary>
public record SalesReportDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string PeriodLabel,
    decimal GrossSales,
    decimal TotalDiscounts,
    decimal TotalTaxes,
    decimal NetRevenue,
    decimal TotalPaidAmount,
    decimal TotalOutstanding,
    int TotalOrders,
    int CompletedOrdersCount,
    int CancelledOrdersCount,
    decimal AverageOrderValue,
    IReadOnlyList<PaymentMethodSalesDto> PaymentMethods,
    IReadOnlyList<OrderTypeSalesDto> OrderTypes,
    IReadOnlyList<TopSellingProductDto> TopProducts,
    IReadOnlyList<DailySalesPointDto> DailyTrend);

/// <summary>
/// Breakdown of sales revenue and transactions per payment method.
/// </summary>
public record PaymentMethodSalesDto(
    PaymentMethod Method,
    string MethodName,
    int TransactionCount,
    decimal TotalAmount,
    double PercentageOfTotal);

/// <summary>
/// Breakdown of sales volume and revenue by order type.
/// </summary>
public record OrderTypeSalesDto(
    OrderType Type,
    string TypeName,
    int OrderCount,
    decimal TotalAmount,
    double PercentageOfTotal);

/// <summary>
/// Ranking entry for top-selling product by quantity and revenue.
/// </summary>
public record TopSellingProductDto(
    string ProductName,
    int QuantitySold,
    decimal TotalRevenue,
    decimal AverageUnitPrice);

/// <summary>
/// Time series point for sales volume and revenue charts/grids.
/// </summary>
public record DailySalesPointDto(
    DateTime Date,
    string DateLabel,
    decimal Revenue,
    int OrderCount);

/// <summary>
/// Request to generate a sales analytics report.
/// </summary>
public record GenerateReportRequest(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? PresetPeriod = null);
