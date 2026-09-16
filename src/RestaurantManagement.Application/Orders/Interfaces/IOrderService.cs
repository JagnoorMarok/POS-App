using RestaurantManagement.Application.Orders.DTOs;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Orders.Interfaces;

/// <summary>
/// Service contract managing POS orders, item snapshots, table occupancy, and lifecycle state.
/// </summary>
public interface IOrderService
{
    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(
        OrderStatus? status = null,
        bool onlyOpen = false,
        CancellationToken cancellationToken = default);

    Task<OrderDto?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OrderDto?> GetOrderByNumberAsync(string orderNumber, CancellationToken cancellationToken = default);

    Task<OrderDto?> GetActiveOrderByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default);

    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);

    Task<OrderDto> AddItemToOrderAsync(Guid orderId, AddOrderItemRequest request, CancellationToken cancellationToken = default);

    Task<OrderDto> UpdateOrderItemQuantityAsync(Guid orderId, UpdateOrderItemQuantityRequest request, CancellationToken cancellationToken = default);

    Task<OrderDto> UpdateOrderItemNotesAsync(Guid orderId, UpdateOrderItemNotesRequest request, CancellationToken cancellationToken = default);

    Task<OrderDto> RemoveOrderItemAsync(Guid orderId, Guid orderItemId, CancellationToken cancellationToken = default);

    Task<OrderDto> UpdateOrderDetailsAsync(Guid orderId, UpdateOrderDetailsRequest request, CancellationToken cancellationToken = default);

    Task<OrderDto> ActivateOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<OrderDto> CancelOrderAsync(Guid orderId, string? reason = null, CancellationToken cancellationToken = default);

    Task<OrderDto> CompleteOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, bool>> GetTableOccupancyMapAsync(CancellationToken cancellationToken = default);
}
