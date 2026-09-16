using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Kitchen.DTOs;

/// <summary>
/// Data Transfer Object representing an order in the Kitchen Display System (KDS).
/// Note: Financial attributes and sensitive employee details are excluded by design.
/// </summary>
public record KitchenOrderDto(
    Guid OrderId,
    string OrderNumber,
    OrderType OrderType,
    OrderStatus Status,
    Guid? RestaurantTableId,
    string? TableNumber,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<KitchenOrderItemDto> Items);

/// <summary>
/// Data Transfer Object representing an individual line item on a kitchen ticket.
/// </summary>
public record KitchenOrderItemDto(
    Guid ItemId,
    Guid? ProductId,
    string ProductNameSnapshot,
    int Quantity,
    string? Notes);
