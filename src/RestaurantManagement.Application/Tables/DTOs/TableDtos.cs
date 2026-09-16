namespace RestaurantManagement.Application.Tables.DTOs;

public record RestaurantTableDto(
    Guid Id,
    string TableNumber,
    int Capacity,
    int DisplayOrder,
    bool IsOccupied,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateTableRequest(
    string TableNumber,
    int Capacity,
    int DisplayOrder = 0,
    bool IsActive = true);

public record UpdateTableRequest(
    Guid Id,
    string TableNumber,
    int Capacity,
    int DisplayOrder = 0,
    bool IsActive = true);
