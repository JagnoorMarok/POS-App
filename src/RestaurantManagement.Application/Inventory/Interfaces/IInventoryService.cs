using RestaurantManagement.Application.Inventory.DTOs;

namespace RestaurantManagement.Application.Inventory.Interfaces;

/// <summary>
/// Service contract managing product stock levels, low-stock threshold alerts, and inventory adjustments.
/// </summary>
public interface IInventoryService
{
    Task<InventorySummaryDto> GetInventorySummaryAsync(
        string? searchQuery = null,
        Guid? categoryId = null,
        bool lowStockOnly = false,
        CancellationToken cancellationToken = default);

    Task<InventoryItemDto> GetItemStockAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<InventoryItemDto> UpdateStockAsync(UpdateStockRequest request, CancellationToken cancellationToken = default);

    Task<InventoryItemDto> AdjustStockAsync(AdjustStockRequest request, CancellationToken cancellationToken = default);
}
