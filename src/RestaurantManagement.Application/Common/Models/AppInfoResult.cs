namespace RestaurantManagement.Application.Common.Models;

/// <summary>
/// Data model containing core application and environment metadata.
/// </summary>
public record AppInfoResult(
    string ApplicationName,
    string Version,
    string Environment,
    string Architecture,
    DateTime ServerTimeUtc
);
