using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Menu.DTOs;
using RestaurantManagement.Application.Menu.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Infrastructure.Persistence;

namespace RestaurantManagement.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        RestaurantDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<ProductService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        var products = await query
            .OrderBy(p => p.Category!.DisplayOrder)
            .ThenBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.CategoryId,
                p.Category != null ? p.Category.Name : "Uncategorized",
                p.IsActive,
                p.IsAvailable,
                p.DisplayOrder,
                p.ImagePath,
                p.CreatedAt,
                p.UpdatedAt))
            .ToListAsync(cancellationToken);

        return products;
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsByCategoryAsync(Guid categoryId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId);

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        var products = await query
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.CategoryId,
                p.Category != null ? p.Category.Name : "Uncategorized",
                p.IsActive,
                p.IsAvailable,
                p.DisplayOrder,
                p.ImagePath,
                p.CreatedAt,
                p.UpdatedAt))
            .ToListAsync(cancellationToken);

        return products;
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Id == id)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.CategoryId,
                p.Category != null ? p.Category.Name : "Uncategorized",
                p.IsActive,
                p.IsAvailable,
                p.DisplayOrder,
                p.ImagePath,
                p.CreatedAt,
                p.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return product;
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateProductDetailsAsync(request.Name, request.Price, request.CategoryId, null, cancellationToken);

        var trimmedName = request.Name.Trim();
        var category = await _dbContext.Categories.FindAsync(new object[] { request.CategoryId }, cancellationToken);
        if (category == null)
        {
            throw new ValidationException(nameof(request.CategoryId), $"Category with ID '{request.CategoryId}' does not exist.");
        }

        var now = _dateTimeProvider.UtcNow;
        var product = new Product(
            Guid.NewGuid(),
            request.CategoryId,
            trimmedName,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.Price,
            now,
            request.DisplayOrder,
            request.IsAvailable,
            request.IsActive,
            string.IsNullOrWhiteSpace(request.ImagePath) ? null : request.ImagePath.Trim());

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created product '{ProductName}' (ID: {ProductId}) under category '{CategoryName}'", product.Name, product.Id, category.Name);

        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.CategoryId,
            category.Name,
            product.IsActive,
            product.IsAvailable,
            product.DisplayOrder,
            product.ImagePath,
            product.CreatedAt,
            product.UpdatedAt);
    }

    public async Task<ProductDto> UpdateProductAsync(UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException(nameof(Product), request.Id);
        }

        await ValidateProductDetailsAsync(request.Name, request.Price, request.CategoryId, request.Id, cancellationToken);

        var category = await _dbContext.Categories.FindAsync(new object[] { request.CategoryId }, cancellationToken);
        if (category == null)
        {
            throw new ValidationException(nameof(request.CategoryId), $"Category with ID '{request.CategoryId}' does not exist.");
        }

        var now = _dateTimeProvider.UtcNow;
        product.UpdateDetails(
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.CategoryId,
            request.DisplayOrder,
            string.IsNullOrWhiteSpace(request.ImagePath) ? null : request.ImagePath.Trim(),
            now);

        if (product.Price != request.Price)
        {
            product.UpdatePrice(request.Price, now);
        }

        if (product.IsAvailable != request.IsAvailable)
        {
            product.SetAvailability(request.IsAvailable, now);
        }

        if (request.IsActive && !product.IsActive)
        {
            product.Activate(now);
        }
        else if (!request.IsActive && product.IsActive)
        {
            product.Deactivate(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated product '{ProductName}' (ID: {ProductId})", product.Name, product.Id);

        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.CategoryId,
            category.Name,
            product.IsActive,
            product.IsAvailable,
            product.DisplayOrder,
            product.ImagePath,
            product.CreatedAt,
            product.UpdatedAt);
    }

    public async Task DeactivateProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException(nameof(Product), id);
        }

        product.Deactivate(_dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deactivated product (ID: {ProductId})", id);
    }

    public async Task ActivateProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException(nameof(Product), id);
        }

        product.Activate(_dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Activated product (ID: {ProductId})", id);
    }

    public async Task SetProductAvailabilityAsync(Guid id, bool isAvailable, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException(nameof(Product), id);
        }

        product.SetAvailability(isAvailable, _dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Changed availability for product (ID: {ProductId}) to {IsAvailable}", id, isAvailable);
    }

    public async Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException(nameof(Product), id);
        }

        // Nullify ProductId in order line items to preserve historical sales records
        var orderItems = await _dbContext.OrderItems
            .Where(oi => oi.ProductId == id)
            .ToListAsync(cancellationToken);

        // Remove any associated inventory settings for this product
        var invSettings = await _dbContext.ApplicationSettings
            .Where(s => s.Key.StartsWith($"inv_stock_{id}") || s.Key.StartsWith($"inv_thresh_{id}"))
            .ToListAsync(cancellationToken);

        if (invSettings.Any())
        {
            _dbContext.ApplicationSettings.RemoveRange(invSettings);
        }

        _dbContext.Products.Remove(product);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted product '{ProductName}' (ID: {ProductId})", product.Name, id);
    }

    private async Task ValidateProductDetailsAsync(
        string name,
        decimal price,
        Guid categoryId,
        Guid? currentProductId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "Product name is required.");
        }

        if (name.Trim().Length > 150)
        {
            throw new ValidationException(nameof(name), "Product name cannot exceed 150 characters.");
        }

        if (price < 0)
        {
            throw new ValidationException(nameof(price), "Product price cannot be negative.");
        }

        var categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == categoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new ValidationException(nameof(categoryId), $"Category with ID '{categoryId}' does not exist.");
        }
    }
}
