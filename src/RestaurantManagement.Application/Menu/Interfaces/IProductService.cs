using RestaurantManagement.Application.Menu.DTOs;

namespace RestaurantManagement.Application.Menu.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> GetProductsByCategoryAsync(Guid categoryId, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ProductDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDto> UpdateProductAsync(UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task DeactivateProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task ActivateProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetProductAvailabilityAsync(Guid id, bool isAvailable, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);
}
