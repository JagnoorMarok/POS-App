using RestaurantManagement.Application.Tables.DTOs;

namespace RestaurantManagement.Application.Tables.Interfaces;

public interface ITableService
{
    Task<IReadOnlyList<RestaurantTableDto>> GetAllTablesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RestaurantTableDto>> GetActiveTablesAsync(CancellationToken cancellationToken = default);
    Task<RestaurantTableDto?> GetTableByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RestaurantTableDto> CreateTableAsync(CreateTableRequest request, CancellationToken cancellationToken = default);
    Task<RestaurantTableDto> UpdateTableAsync(UpdateTableRequest request, CancellationToken cancellationToken = default);
    Task DeactivateTableAsync(Guid id, CancellationToken cancellationToken = default);
    Task ActivateTableAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetTableOccupancyAsync(Guid id, bool isOccupied, CancellationToken cancellationToken = default);
    Task DeleteTableAsync(Guid id, CancellationToken cancellationToken = default);
}
