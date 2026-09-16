using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Orders.DTOs;

public record OrderItemDto(
    Guid Id,
    Guid? ProductId,
    string ProductNameSnapshot,
    decimal UnitPrice,
    int Quantity,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string? Notes);

public record OrderDto(
    Guid Id,
    string OrderNumber,
    OrderType OrderType,
    OrderStatus Status,
    Guid? RestaurantTableId,
    string? TableNumber,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<OrderItemDto> Items);

public record CreateOrderRequest(
    OrderType OrderType,
    Guid? RestaurantTableId = null,
    string? Notes = null);

public record AddOrderItemRequest(
    Guid ProductId,
    int Quantity = 1,
    string? Notes = null);

public record UpdateOrderItemQuantityRequest(
    Guid OrderItemId,
    int Quantity);

public record UpdateOrderItemNotesRequest(
    Guid OrderItemId,
    string? Notes);

public record UpdateOrderDetailsRequest(
    OrderType OrderType,
    Guid? RestaurantTableId,
    string? Notes,
    decimal DiscountAmount = 0m);
