using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Inventory.DTOs;
using RestaurantManagement.Application.Inventory.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;

namespace RestaurantManagement.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private const int DefaultStockQuantity = 50;
    private const int DefaultLowStockThreshold = 10;
    private const string StockKeyPrefix = "inv_stock_";
    private const string ThresholdKeyPrefix = "inv_thresh_";
    private const string LastRestockedKeyPrefix = "inv_restock_";

    private readonly RestaurantDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        RestaurantDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        ILogger<InventoryService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<InventorySummaryDto> GetInventorySummaryAsync(
        string? searchQuery = null,
        Guid? categoryId = null,
        bool lowStockOnly = false,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(isWrite: false);

        var productsQuery = _dbContext.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var search = searchQuery.Trim().ToLower();
            productsQuery = productsQuery.Where(p =>
                p.Name.ToLower().Contains(search) ||
                (p.Description != null && p.Description.ToLower().Contains(search)));
        }

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
        }

        var products = await productsQuery
            .OrderBy(p => p.Category != null ? p.Category.DisplayOrder : 0)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        // Fetch all inventory settings
        var settings = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(s => s.Key.StartsWith("inv_"))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        var items = new List<InventoryItemDto>();
        int inStockCount = 0;
        int lowStockCount = 0;
        int outOfStockCount = 0;

        foreach (var product in products)
        {
            var stockKey = $"{StockKeyPrefix}{product.Id}";
            var threshKey = $"{ThresholdKeyPrefix}{product.Id}";
            var restockKey = $"{LastRestockedKeyPrefix}{product.Id}";

            int currentStock = settings.TryGetValue(stockKey, out var stockVal) && int.TryParse(stockVal, out var parsedStock)
                ? parsedStock
                : (product.IsAvailable ? DefaultStockQuantity : 0);

            int threshold = settings.TryGetValue(threshKey, out var threshVal) && int.TryParse(threshVal, out var parsedThresh)
                ? parsedThresh
                : DefaultLowStockThreshold;

            DateTime? lastRestocked = settings.TryGetValue(restockKey, out var restockVal) && DateTime.TryParse(restockVal, out var parsedDate)
                ? parsedDate
                : null;

            string status;
            if (currentStock <= 0 || !product.IsAvailable)
            {
                status = "Out of Stock";
                outOfStockCount++;
            }
            else if (currentStock <= threshold)
            {
                status = "Low Stock";
                lowStockCount++;
            }
            else
            {
                status = "In Stock";
                inStockCount++;
            }

            if (!lowStockOnly || status != "In Stock")
            {
                items.Add(new InventoryItemDto(
                    product.Id,
                    product.Name,
                    product.CategoryId,
                    product.Category?.Name ?? "Uncategorized",
                    product.Price,
                    currentStock,
                    threshold,
                    product.IsAvailable,
                    product.IsActive,
                    status,
                    lastRestocked));
            }
        }

        return new InventorySummaryDto(
            products.Count,
            inStockCount,
            lowStockCount,
            outOfStockCount,
            items);
    }

    public async Task<InventoryItemDto> GetItemStockAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(isWrite: false);

        var product = await _dbContext.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException(nameof(Product), productId);
        }

        var stockKey = $"{StockKeyPrefix}{product.Id}";
        var threshKey = $"{ThresholdKeyPrefix}{product.Id}";
        var restockKey = $"{LastRestockedKeyPrefix}{product.Id}";

        var stockSetting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == stockKey, cancellationToken);
        var threshSetting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == threshKey, cancellationToken);
        var restockSetting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == restockKey, cancellationToken);

        int currentStock = stockSetting != null && int.TryParse(stockSetting.Value, out var parsedStock)
            ? parsedStock
            : (product.IsAvailable ? DefaultStockQuantity : 0);

        int threshold = threshSetting != null && int.TryParse(threshSetting.Value, out var parsedThresh)
            ? parsedThresh
            : DefaultLowStockThreshold;

        DateTime? lastRestocked = restockSetting != null && DateTime.TryParse(restockSetting.Value, out var parsedDate)
            ? parsedDate
            : null;

        string status = currentStock <= 0 || !product.IsAvailable
            ? "Out of Stock"
            : (currentStock <= threshold ? "Low Stock" : "In Stock");

        return new InventoryItemDto(
            product.Id,
            product.Name,
            product.CategoryId,
            product.Category?.Name ?? "Uncategorized",
            product.Price,
            currentStock,
            threshold,
            product.IsAvailable,
            product.IsActive,
            status,
            lastRestocked);
    }

    public async Task<InventoryItemDto> UpdateStockAsync(UpdateStockRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(isWrite: true);

        if (request.NewStock < 0)
        {
            throw new ValidationException(nameof(request.NewStock), "Stock quantity cannot be negative.");
        }

        var product = await _dbContext.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException(nameof(Product), request.ProductId);
        }

        var now = _dateTimeProvider.UtcNow;
        var stockKey = $"{StockKeyPrefix}{product.Id}";
        var threshKey = $"{ThresholdKeyPrefix}{product.Id}";
        var restockKey = $"{LastRestockedKeyPrefix}{product.Id}";

        // Update Stock Setting
        var stockSetting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == stockKey, cancellationToken);
        if (stockSetting == null)
        {
            stockSetting = new ApplicationSetting(Guid.NewGuid(), stockKey, request.NewStock.ToString(), now);
            _dbContext.ApplicationSettings.Add(stockSetting);
        }
        else
        {
            stockSetting.UpdateValue(request.NewStock.ToString(), now);
        }

        // Update Threshold if provided
        int threshold = DefaultLowStockThreshold;
        if (request.LowStockThreshold.HasValue && request.LowStockThreshold.Value >= 0)
        {
            threshold = request.LowStockThreshold.Value;
            var threshSetting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == threshKey, cancellationToken);
            if (threshSetting == null)
            {
                threshSetting = new ApplicationSetting(Guid.NewGuid(), threshKey, threshold.ToString(), now);
                _dbContext.ApplicationSettings.Add(threshSetting);
            }
            else
            {
                threshSetting.UpdateValue(threshold.ToString(), now);
            }
        }
        else
        {
            var existingThresh = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == threshKey, cancellationToken);
            if (existingThresh != null && int.TryParse(existingThresh.Value, out var parsed))
            {
                threshold = parsed;
            }
        }

        // Update Restock Timestamp
        var restockSetting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == restockKey, cancellationToken);
        if (restockSetting == null)
        {
            restockSetting = new ApplicationSetting(Guid.NewGuid(), restockKey, now.ToString("o"), now);
            _dbContext.ApplicationSettings.Add(restockSetting);
        }
        else
        {
            restockSetting.UpdateValue(now.ToString("o"), now);
        }

        // Synchronize product availability: 0 stock means out of stock
        bool newAvailability = request.NewStock > 0;
        if (product.IsAvailable != newAvailability)
        {
            product.SetAvailability(newAvailability, now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated stock for {Product} to {NewStock} (Staff: {Staff})",
            product.Name, request.NewStock, _currentUserService.Username ?? "System");

        string status = request.NewStock <= 0
            ? "Out of Stock"
            : (request.NewStock <= threshold ? "Low Stock" : "In Stock");

        return new InventoryItemDto(
            product.Id,
            product.Name,
            product.CategoryId,
            product.Category?.Name ?? "Uncategorized",
            product.Price,
            request.NewStock,
            threshold,
            product.IsAvailable,
            product.IsActive,
            status,
            now);
    }

    public async Task<InventoryItemDto> AdjustStockAsync(AdjustStockRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(isWrite: true);

        var product = await _dbContext.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException(nameof(Product), request.ProductId);
        }

        var stockKey = $"{StockKeyPrefix}{product.Id}";
        var stockSetting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == stockKey, cancellationToken);

        int currentStock = stockSetting != null && int.TryParse(stockSetting.Value, out var parsedStock)
            ? parsedStock
            : (product.IsAvailable ? DefaultStockQuantity : 0);

        int newStock = Math.Max(0, currentStock + request.QuantityDelta);

        return await UpdateStockAsync(new UpdateStockRequest(
            request.ProductId,
            newStock,
            Reason: request.Reason), cancellationToken);
    }

    private void EnsureAuthorized(bool isWrite)
    {
        if (_currentUserService.IsAuthenticated)
        {
            var isAllowed = _authorizationService.CanAccessFeature(_currentUserService.Role, "inventory");
            if (!isAllowed)
            {
                throw new UnauthorizedAccessException($"Role '{_currentUserService.Role}' is not authorized to access Inventory.");
            }
        }
    }
}
