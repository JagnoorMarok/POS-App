using RestaurantManagement.Application.Settings.DTOs;

namespace RestaurantManagement.Application.Settings.Interfaces;

/// <summary>
/// Service contract managing restaurant business profile, application configuration, and system diagnostics.
/// </summary>
public interface ISettingsService
{
    Task<RestaurantProfileDto> GetRestaurantProfileAsync(CancellationToken cancellationToken = default);

    Task<RestaurantProfileDto> UpdateRestaurantProfileAsync(UpdateRestaurantProfileRequest request, CancellationToken cancellationToken = default);

    Task<SystemDiagnosticsDto> GetSystemDiagnosticsAsync(CancellationToken cancellationToken = default);

    Task<AppConfigDto> GetAppConfigAsync(CancellationToken cancellationToken = default);

    Task<AppConfigDto> UpdateAppConfigAsync(UpdateAppConfigRequest request, CancellationToken cancellationToken = default);
}
