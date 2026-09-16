namespace RestaurantManagement.Application.Menu.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    Guid CategoryId,
    string CategoryName,
    bool IsActive,
    bool IsAvailable,
    int DisplayOrder,
    string? ImagePath,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateProductRequest(
    string Name,
    decimal Price,
    Guid CategoryId,
    string? Description = null,
    int DisplayOrder = 0,
    bool IsActive = true,
    bool IsAvailable = true,
    string? ImagePath = null);

public record UpdateProductRequest(
    Guid Id,
    string Name,
    decimal Price,
    Guid CategoryId,
    string? Description = null,
    int DisplayOrder = 0,
    bool IsActive = true,
    bool IsAvailable = true,
    string? ImagePath = null);
