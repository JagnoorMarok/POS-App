using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Billing.DTOs;

/// <summary>
/// Full billing details for an order, including restaurant branding, line items, breakdown, and payment status.
/// </summary>
public record BillDto(
    Guid OrderId,
    string OrderNumber,
    OrderType OrderType,
    OrderStatus OrderStatus,
    Guid? RestaurantTableId,
    string? TableNumber,
    string RestaurantName,
    string? RestaurantAddress,
    string? RestaurantPhone,
    string CurrencyCode,
    string? TaxNumber,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    bool IsFullyPaid,
    IReadOnlyList<BillItemDto> Items,
    IReadOnlyList<PaymentDto> Payments);

/// <summary>
/// Individual line item on a bill, capturing snapshot product name, quantities, and prices.
/// </summary>
public record BillItemDto(
    Guid OrderItemId,
    Guid? ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string? Notes);

/// <summary>
/// Lightweight summary of an order awaiting billing or payment settlement.
/// </summary>
public record BillSummaryDto(
    Guid OrderId,
    string OrderNumber,
    OrderType OrderType,
    OrderStatus OrderStatus,
    Guid? RestaurantTableId,
    string? TableNumber,
    DateTime CreatedAt,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    bool IsFullyPaid,
    int ItemCount);

/// <summary>
/// DTO representing a recorded payment on an order.
/// </summary>
public record PaymentDto(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    PaymentStatus Status,
    string? TransactionReference,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    decimal? CashTendered = null,
    decimal? ChangeReturned = null);

/// <summary>
/// Summary of total bill amount, amount settled, and remaining balance.
/// </summary>
public record PaymentSummaryDto(
    Guid OrderId,
    string OrderNumber,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    bool IsFullyPaid,
    IReadOnlyList<PaymentDto> Payments);

/// <summary>
/// Request to record a local payment against an order.
/// </summary>
public record RecordPaymentRequest(
    Guid OrderId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? TransactionReference = null,
    decimal? CashTendered = null);

/// <summary>
/// Request to apply or adjust an order-level discount.
/// </summary>
public record ApplyDiscountRequest(
    Guid OrderId,
    decimal DiscountAmount);
