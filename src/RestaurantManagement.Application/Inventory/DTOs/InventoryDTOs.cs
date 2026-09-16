namespace RestaurantManagement.Application.Inventory.DTOs;

/// <summary>
/// DTO representing a product item in the inventory catalog with its stock level and alert status.
/// </summary>
public record InventoryItemDto(
    Guid ProductId,
    string ProductName,
    Guid CategoryId,
    string CategoryName,
    decimal UnitPrice,
    int CurrentStock,
    int LowStockThreshold,
    bool IsAvailable,
    bool IsActive,
    string StockStatus,
    DateTime? LastRestockedAt);

/// <summary>
/// Summary DTO providing high-level inventory KPIs.
/// </summary>
public record InventorySummaryDto(
    int TotalItems,
    int InStockCount,
    int LowStockCount,
    int OutOfStockCount,
    IReadOnlyList<InventoryItemDto> Items);

/// <summary>
/// Request to explicitly set stock quantity and threshold for a product.
/// </summary>
public record UpdateStockRequest(
    Guid ProductId,
    int NewStock,
    int? LowStockThreshold = null,
    string? Reason = null);

/// <summary>
/// Request to adjust stock by a relative delta (positive for restock, negative for waste/spoilage).
/// </summary>
public record AdjustStockRequest(
    Guid ProductId,
    int QuantityDelta,
    string? Reason = null);
