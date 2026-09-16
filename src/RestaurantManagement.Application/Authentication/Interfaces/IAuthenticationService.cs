using RestaurantManagement.Application.Authentication.DTOs;

namespace RestaurantManagement.Application.Authentication.Interfaces;

public interface IAuthenticationService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<bool> HasAnyEmployeesAsync(CancellationToken cancellationToken = default);
    Task<LoginResult> InitializeFirstAdminAsync(CreateFirstAdminRequest request, CancellationToken cancellationToken = default);
}
