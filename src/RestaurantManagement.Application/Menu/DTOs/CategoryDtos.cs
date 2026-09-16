namespace RestaurantManagement.Application.Menu.DTOs;

public record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    int ProductCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateCategoryRequest(
    string Name,
    string? Description = null,
    int DisplayOrder = 0,
    bool IsActive = true);

public record UpdateCategoryRequest(
    Guid Id,
    string Name,
    string? Description = null,
    int DisplayOrder = 0,
    bool IsActive = true);
