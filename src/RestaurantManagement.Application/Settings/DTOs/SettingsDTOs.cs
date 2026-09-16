namespace RestaurantManagement.Application.Settings.DTOs;

/// <summary>
/// DTO representing the restaurant profile branding and tax details.
/// </summary>
public record RestaurantProfileDto(
    Guid Id,
    string RestaurantName,
    string? Address,
    string? PhoneNumber,
    string CurrencyCode,
    string CurrencySymbol,
    string? GstOrTaxNumber,
    decimal DefaultTaxRatePercent,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Request to update the restaurant profile and tax configuration.
/// </summary>
public record UpdateRestaurantProfileRequest(
    string RestaurantName,
    string? Address,
    string? PhoneNumber,
    string CurrencyCode,
    string CurrencySymbol,
    string? GstOrTaxNumber,
    decimal DefaultTaxRatePercent);

/// <summary>
/// DTO providing real-time system and SQLite database diagnostics.
/// </summary>
public record SystemDiagnosticsDto(
    string DatabasePath,
    long DatabaseSizeBytes,
    string FormattedDatabaseSize,
    bool IsDatabaseHealthy,
    int TotalOrders,
    int TotalProducts,
    int TotalCategories,
    int TotalEmployees,
    int TotalTables,
    DateTime ServerTimeUtc,
    string EnvironmentName,
    string AppVersion,
    string Architecture);

/// <summary>
/// DTO for configurable local hardware & operational settings.
/// </summary>
public record AppConfigDto(
    int KdsRefreshIntervalSeconds,
    string DefaultOrderType,
    string ReceiptHeaderMessage,
    string ReceiptFooterMessage);

/// <summary>
/// Request to update operational app settings.
/// </summary>
public record UpdateAppConfigRequest(
    int KdsRefreshIntervalSeconds,
    string DefaultOrderType,
    string ReceiptHeaderMessage,
    string ReceiptFooterMessage);
