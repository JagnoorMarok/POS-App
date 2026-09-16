using RestaurantManagement.Application.Kitchen.DTOs;

namespace RestaurantManagement.Application.Kitchen.Interfaces;

/// <summary>
/// Service contract managing kitchen ticket queues and status transitions.
/// </summary>
public interface IKitchenService
{
    Task<IReadOnlyList<KitchenOrderDto>> GetKitchenOrdersAsync(CancellationToken cancellationToken = default);

    Task<KitchenOrderDto?> GetKitchenOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<KitchenOrderDto> StartPreparationAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<KitchenOrderDto> MarkOrderReadyAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<KitchenOrderDto> MarkOrderServedAsync(Guid orderId, CancellationToken cancellationToken = default);
}
