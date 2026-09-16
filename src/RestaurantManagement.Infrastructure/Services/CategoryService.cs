using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Menu.DTOs;
using RestaurantManagement.Application.Menu.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Infrastructure.Persistence;

namespace RestaurantManagement.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(
        RestaurantDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<CategoryService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Categories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var categories = await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.DisplayOrder,
                c.IsActive,
                c.Products.Count,
                c.CreatedAt,
                c.UpdatedAt))
            .ToListAsync(cancellationToken);

        return categories;
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.DisplayOrder,
                c.IsActive,
                c.Products.Count,
                c.CreatedAt,
                c.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return category;
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ValidateCategoryRequest(request.Name);

        var trimmedName = request.Name.Trim();

        var nameExists = await _dbContext.Categories
            .AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (nameExists)
        {
            throw new ValidationException(nameof(request.Name), $"A category named '{trimmedName}' already exists.");
        }

        var now = _dateTimeProvider.UtcNow;
        var category = new Category(
            Guid.NewGuid(),
            trimmedName,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.DisplayOrder,
            now,
            request.IsActive);

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created category '{CategoryName}' (ID: {CategoryId})", category.Name, category.Id);

        return new CategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.DisplayOrder,
            category.IsActive,
            0,
            category.CreatedAt,
            category.UpdatedAt);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ValidateCategoryRequest(request.Name);

        var category = await _dbContext.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category == null)
        {
            throw new NotFoundException(nameof(Category), request.Id);
        }

        var trimmedName = request.Name.Trim();

        var nameExists = await _dbContext.Categories
            .AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower() && c.Id != request.Id, cancellationToken);

        if (nameExists)
        {
            throw new ValidationException(nameof(request.Name), $"A category named '{trimmedName}' already exists.");
        }

        var now = _dateTimeProvider.UtcNow;
        category.UpdateDetails(
            trimmedName,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.DisplayOrder,
            now);

        if (request.IsActive && !category.IsActive)
        {
            category.Activate(now);
        }
        else if (!request.IsActive && category.IsActive)
        {
            category.Deactivate(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated category '{CategoryName}' (ID: {CategoryId})", category.Name, category.Id);

        return new CategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.DisplayOrder,
            category.IsActive,
            category.Products.Count,
            category.CreatedAt,
            category.UpdatedAt);
    }

    public async Task DeactivateCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FindAsync(new object[] { id }, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException(nameof(Category), id);
        }

        category.Deactivate(_dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deactivated category (ID: {CategoryId})", id);
    }

    public async Task ActivateCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FindAsync(new object[] { id }, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException(nameof(Category), id);
        }

        category.Activate(_dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Activated category (ID: {CategoryId})", id);
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
        {
            throw new NotFoundException(nameof(Category), id);
        }

        if (category.Products.Any())
        {
            throw new ValidationException(nameof(id), $"Cannot delete Category '{category.Name}' because it contains {category.Products.Count} product(s). Please delete or reassign the products first.");
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted category '{CategoryName}' (ID: {CategoryId})", category.Name, id);
    }

    private static void ValidateCategoryRequest(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "Category name is required.");
        }

        if (name.Trim().Length > 100)
        {
            throw new ValidationException(nameof(name), "Category name cannot exceed 100 characters.");
        }
    }
}
