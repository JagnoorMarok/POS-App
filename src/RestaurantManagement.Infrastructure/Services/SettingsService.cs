using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Settings.DTOs;
using RestaurantManagement.Application.Settings.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Infrastructure.Persistence;

namespace RestaurantManagement.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private const string TaxRateKey = "app_default_tax_rate";
    private const string CurrencySymbolKey = "app_currency_symbol";
    private const string KdsIntervalKey = "app_kds_refresh_interval";
    private const string DefaultOrderTypeKey = "app_default_order_type";
    private const string ReceiptHeaderKey = "app_receipt_header";
    private const string ReceiptFooterKey = "app_receipt_footer";

    private readonly RestaurantDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAppInfoService _appInfoService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(
        RestaurantDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IAppInfoService appInfoService,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        ILogger<SettingsService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _appInfoService = appInfoService;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<RestaurantProfileDto> GetRestaurantProfileAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(isWrite: false);

        var profile = await _dbContext.RestaurantProfiles.FirstOrDefaultAsync(cancellationToken);
        if (profile == null)
        {
            var now = _dateTimeProvider.UtcNow;
            profile = new RestaurantProfile(
                Guid.NewGuid(),
                "The Grand Bistro",
                now,
                "123 Restaurant Way, Suite 100",
                "+91 98765 43210",
                "INR",
                "GSTIN27AAAAA0000A1Z5");

            _dbContext.RestaurantProfiles.Add(profile);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Fetch tax rate & currency symbol from settings if available
        var taxRateSetting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == TaxRateKey, cancellationToken);
        var symbolSetting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == CurrencySymbolKey, cancellationToken);

        decimal defaultTaxRate = 5.0m;
        if (taxRateSetting != null && decimal.TryParse(taxRateSetting.Value, out var parsedTax))
        {
            defaultTaxRate = parsedTax;
        }

        string currencySymbol = symbolSetting?.Value ?? "₹";

        return new RestaurantProfileDto(
            profile.Id,
            profile.RestaurantName,
            profile.Address,
            profile.PhoneNumber,
            profile.CurrencyCode,
            currencySymbol,
            profile.GstOrTaxNumber,
            defaultTaxRate,
            profile.CreatedAt,
            profile.UpdatedAt);
    }

    public async Task<RestaurantProfileDto> UpdateRestaurantProfileAsync(UpdateRestaurantProfileRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(isWrite: true);

        if (string.IsNullOrWhiteSpace(request.RestaurantName))
        {
            throw new ValidationException(nameof(request.RestaurantName), "Restaurant name cannot be empty.");
        }

        if (request.DefaultTaxRatePercent < 0 || request.DefaultTaxRatePercent > 100)
        {
            throw new ValidationException(nameof(request.DefaultTaxRatePercent), "Tax rate must be between 0% and 100%.");
        }

        var profile = await _dbContext.RestaurantProfiles.FirstOrDefaultAsync(cancellationToken);
        var now = _dateTimeProvider.UtcNow;

        if (profile == null)
        {
            profile = new RestaurantProfile(
                Guid.NewGuid(),
                request.RestaurantName,
                now,
                request.Address,
                request.PhoneNumber,
                request.CurrencyCode,
                request.GstOrTaxNumber);

            _dbContext.RestaurantProfiles.Add(profile);
        }
        else
        {
            profile.UpdateProfile(
                request.RestaurantName,
                request.Address,
                request.PhoneNumber,
                request.CurrencyCode,
                request.GstOrTaxNumber,
                now);
        }

        // Update Tax Rate & Currency Symbol
        var taxRateSetting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == TaxRateKey, cancellationToken);
        if (taxRateSetting == null)
        {
            taxRateSetting = new ApplicationSetting(Guid.NewGuid(), TaxRateKey, request.DefaultTaxRatePercent.ToString("F2"), now);
            _dbContext.ApplicationSettings.Add(taxRateSetting);
        }
        else
        {
            taxRateSetting.UpdateValue(request.DefaultTaxRatePercent.ToString("F2"), now);
        }

        var symbolSetting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == CurrencySymbolKey, cancellationToken);
        if (symbolSetting == null)
        {
            symbolSetting = new ApplicationSetting(Guid.NewGuid(), CurrencySymbolKey, request.CurrencySymbol, now);
            _dbContext.ApplicationSettings.Add(symbolSetting);
        }
        else
        {
            symbolSetting.UpdateValue(request.CurrencySymbol, now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated restaurant profile: {Name}, Tax: {Tax}% (Staff: {Staff})",
            profile.RestaurantName, request.DefaultTaxRatePercent, _currentUserService.Username ?? "System");

        return new RestaurantProfileDto(
            profile.Id,
            profile.RestaurantName,
            profile.Address,
            profile.PhoneNumber,
            profile.CurrencyCode,
            request.CurrencySymbol,
            profile.GstOrTaxNumber,
            request.DefaultTaxRatePercent,
            profile.CreatedAt,
            profile.UpdatedAt);
    }

    public async Task<SystemDiagnosticsDto> GetSystemDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(isWrite: false);

        var appInfo = _appInfoService.GetAppInfo();
        string dbPath = _dbContext.Database.GetDbConnection().ConnectionString;

        long dbSizeBytes = 0;
        string formattedSize = "Unknown";
        try
        {
            var dataSource = dbPath.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (File.Exists(dataSource))
            {
                var fi = new FileInfo(dataSource);
                dbSizeBytes = fi.Length;
                formattedSize = dbSizeBytes < 1024 * 1024
                    ? $"{dbSizeBytes / 1024.0:F1} KB"
                    : $"{dbSizeBytes / (1024.0 * 1024.0):F2} MB";
            }
        }
        catch
        {
            // Ignored if connection string format varies
        }

        bool isHealthy = await _dbContext.Database.CanConnectAsync(cancellationToken);
        int totalOrders = await _dbContext.Orders.CountAsync(cancellationToken);
        int totalProducts = await _dbContext.Products.CountAsync(cancellationToken);
        int totalCategories = await _dbContext.Categories.CountAsync(cancellationToken);
        int totalEmployees = await _dbContext.Employees.CountAsync(cancellationToken);
        int totalTables = await _dbContext.RestaurantTables.CountAsync(cancellationToken);

        return new SystemDiagnosticsDto(
            dbPath,
            dbSizeBytes,
            formattedSize,
            isHealthy,
            totalOrders,
            totalProducts,
            totalCategories,
            totalEmployees,
            totalTables,
            _dateTimeProvider.UtcNow,
            appInfo.Environment,
            appInfo.Version,
            appInfo.Architecture);
    }

    public async Task<AppConfigDto> GetAppConfigAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(isWrite: false);

        var settings = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(s => s.Key.StartsWith("app_"))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        int kdsInterval = settings.TryGetValue(KdsIntervalKey, out var kdsVal) && int.TryParse(kdsVal, out var kds)
            ? kds
            : 5;

        string defaultOrderType = settings.TryGetValue(DefaultOrderTypeKey, out var ot) ? ot : "DineIn";
        string header = settings.TryGetValue(ReceiptHeaderKey, out var h) ? h : "Thank you for dining with us!";
        string footer = settings.TryGetValue(ReceiptFooterKey, out var f) ? f : "Please visit again. GST Included.";

        return new AppConfigDto(kdsInterval, defaultOrderType, header, footer);
    }

    public async Task<AppConfigDto> UpdateAppConfigAsync(UpdateAppConfigRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(isWrite: true);

        var now = _dateTimeProvider.UtcNow;

        await UpsertSettingAsync(KdsIntervalKey, request.KdsRefreshIntervalSeconds.ToString(), now, cancellationToken);
        await UpsertSettingAsync(DefaultOrderTypeKey, request.DefaultOrderType, now, cancellationToken);
        await UpsertSettingAsync(ReceiptHeaderKey, request.ReceiptHeaderMessage, now, cancellationToken);
        await UpsertSettingAsync(ReceiptFooterKey, request.ReceiptFooterMessage, now, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated application hardware/operational configuration (Staff: {Staff})",
            _currentUserService.Username ?? "System");

        return new AppConfigDto(
            request.KdsRefreshIntervalSeconds,
            request.DefaultOrderType,
            request.ReceiptHeaderMessage,
            request.ReceiptFooterMessage);
    }

    private async Task UpsertSettingAsync(string key, string value, DateTime now, CancellationToken cancellationToken)
    {
        var setting = await _dbContext.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting == null)
        {
            setting = new ApplicationSetting(Guid.NewGuid(), key, value, now);
            _dbContext.ApplicationSettings.Add(setting);
        }
        else
        {
            setting.UpdateValue(value, now);
        }
    }

    private void EnsureAuthorized(bool isWrite)
    {
        if (_currentUserService.IsAuthenticated)
        {
            var isAllowed = _authorizationService.CanAccessFeature(_currentUserService.Role, "settings");
            if (!isAllowed)
            {
                throw new UnauthorizedAccessException($"Role '{_currentUserService.Role}' is not authorized to access Settings.");
            }
        }
    }
}
