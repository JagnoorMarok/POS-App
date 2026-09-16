using System.Windows.Input;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Settings.DTOs;
using RestaurantManagement.Application.Settings.Interfaces;

namespace RestaurantManagement.Desktop.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsViewModel> _logger;

    private bool _isLoading;
    private string? _errorMessage;
    private string? _successMessage;
    private string _selectedTab = "Profile"; // Profile, Diagnostics, Config

    // Tab 1: Restaurant Profile & Tax
    private Guid _profileId;
    private string _restaurantName = "The Grand Bistro";
    private string? _address = "123 Restaurant Way";
    private string? _phoneNumber = "+91 98765 43210";
    private string _currencyCode = "INR";
    private string _currencySymbol = "₹";
    private string? _gstOrTaxNumber = "GSTIN27AAAAA0000A1Z5";
    private decimal _defaultTaxRatePercent = 5.0m;

    // Tab 2: System Diagnostics
    private string _databasePath = string.Empty;
    private string _formattedDatabaseSize = "0 KB";
    private bool _isDatabaseHealthy = true;
    private int _totalOrders;
    private int _totalProducts;
    private int _totalCategories;
    private int _totalEmployees;
    private int _totalTables;
    private string _serverTimeUtc = string.Empty;
    private string _environmentName = "Production";
    private string _appVersion = "1.0.0";
    private string _architecture = "x64";

    // Tab 3: Operational Config
    private int _kdsRefreshIntervalSeconds = 5;
    private string _defaultOrderType = "DineIn";
    private string _receiptHeaderMessage = "Thank you for dining with us!";
    private string _receiptFooterMessage = "Please visit again. GST Included.";

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string? SuccessMessage
    {
        get => _successMessage;
        set => SetProperty(ref _successMessage, value);
    }

    public string SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    // Profile Properties
    public string RestaurantName
    {
        get => _restaurantName;
        set => SetProperty(ref _restaurantName, value);
    }

    public string? Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public string? PhoneNumber
    {
        get => _phoneNumber;
        set => SetProperty(ref _phoneNumber, value);
    }

    public string CurrencyCode
    {
        get => _currencyCode;
        set => SetProperty(ref _currencyCode, value);
    }

    public string CurrencySymbol
    {
        get => _currencySymbol;
        set => SetProperty(ref _currencySymbol, value);
    }

    public string? GstOrTaxNumber
    {
        get => _gstOrTaxNumber;
        set => SetProperty(ref _gstOrTaxNumber, value);
    }

    public decimal DefaultTaxRatePercent
    {
        get => _defaultTaxRatePercent;
        set => SetProperty(ref _defaultTaxRatePercent, value);
    }

    // Diagnostics Properties
    public string DatabasePath
    {
        get => _databasePath;
        set => SetProperty(ref _databasePath, value);
    }

    public string FormattedDatabaseSize
    {
        get => _formattedDatabaseSize;
        set => SetProperty(ref _formattedDatabaseSize, value);
    }

    public bool IsDatabaseHealthy
    {
        get => _isDatabaseHealthy;
        set => SetProperty(ref _isDatabaseHealthy, value);
    }

    public int TotalOrders
    {
        get => _totalOrders;
        set => SetProperty(ref _totalOrders, value);
    }

    public int TotalProducts
    {
        get => _totalProducts;
        set => SetProperty(ref _totalProducts, value);
    }

    public int TotalCategories
    {
        get => _totalCategories;
        set => SetProperty(ref _totalCategories, value);
    }

    public int TotalEmployees
    {
        get => _totalEmployees;
        set => SetProperty(ref _totalEmployees, value);
    }

    public int TotalTables
    {
        get => _totalTables;
        set => SetProperty(ref _totalTables, value);
    }

    public string ServerTimeUtc
    {
        get => _serverTimeUtc;
        set => SetProperty(ref _serverTimeUtc, value);
    }

    public string EnvironmentName
    {
        get => _environmentName;
        set => SetProperty(ref _environmentName, value);
    }

    public string AppVersion
    {
        get => _appVersion;
        set => SetProperty(ref _appVersion, value);
    }

    public string Architecture
    {
        get => _architecture;
        set => SetProperty(ref _architecture, value);
    }

    // Config Properties
    public int KdsRefreshIntervalSeconds
    {
        get => _kdsRefreshIntervalSeconds;
        set => SetProperty(ref _kdsRefreshIntervalSeconds, value);
    }

    public string DefaultOrderType
    {
        get => _defaultOrderType;
        set => SetProperty(ref _defaultOrderType, value);
    }

    public string ReceiptHeaderMessage
    {
        get => _receiptHeaderMessage;
        set => SetProperty(ref _receiptHeaderMessage, value);
    }

    public string ReceiptFooterMessage
    {
        get => _receiptFooterMessage;
        set => SetProperty(ref _receiptFooterMessage, value);
    }

    public ICommand SelectTabCommand { get; }
    public ICommand RefreshAllCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand SaveAppConfigCommand { get; }

    public SettingsViewModel(
        ISettingsService settingsService,
        ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _logger = logger;

        SelectTabCommand = new RelayCommand<string>(tab =>
        {
            if (!string.IsNullOrEmpty(tab))
            {
                SelectedTab = tab;
            }
        });

        RefreshAllCommand = new RelayCommand(async _ => await LoadAllSettingsAsync());
        SaveProfileCommand = new RelayCommand(async _ => await SaveProfileAsync());
        SaveAppConfigCommand = new RelayCommand(async _ => await SaveAppConfigAsync());
    }

    public async Task LoadAllSettingsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // 1. Profile
            var profile = await _settingsService.GetRestaurantProfileAsync();
            _profileId = profile.Id;
            RestaurantName = profile.RestaurantName;
            Address = profile.Address;
            PhoneNumber = profile.PhoneNumber;
            CurrencyCode = profile.CurrencyCode;
            CurrencySymbol = profile.CurrencySymbol;
            GstOrTaxNumber = profile.GstOrTaxNumber;
            DefaultTaxRatePercent = profile.DefaultTaxRatePercent;

            // 2. Diagnostics
            var diag = await _settingsService.GetSystemDiagnosticsAsync();
            DatabasePath = diag.DatabasePath;
            FormattedDatabaseSize = diag.FormattedDatabaseSize;
            IsDatabaseHealthy = diag.IsDatabaseHealthy;
            TotalOrders = diag.TotalOrders;
            TotalProducts = diag.TotalProducts;
            TotalCategories = diag.TotalCategories;
            TotalEmployees = diag.TotalEmployees;
            TotalTables = diag.TotalTables;
            ServerTimeUtc = diag.ServerTimeUtc.ToString("yyyy-MM-dd HH:mm:ss UTC");
            EnvironmentName = diag.EnvironmentName;
            AppVersion = diag.AppVersion;
            Architecture = diag.Architecture;

            // 3. Operational Config
            var config = await _settingsService.GetAppConfigAsync();
            KdsRefreshIntervalSeconds = config.KdsRefreshIntervalSeconds;
            DefaultOrderType = config.DefaultOrderType;
            ReceiptHeaderMessage = config.ReceiptHeaderMessage;
            ReceiptFooterMessage = config.ReceiptFooterMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings.");
            ErrorMessage = "Failed to load settings. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveProfileAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var req = new UpdateRestaurantProfileRequest(
                RestaurantName,
                Address,
                PhoneNumber,
                CurrencyCode,
                CurrencySymbol,
                GstOrTaxNumber,
                DefaultTaxRatePercent);

            await _settingsService.UpdateRestaurantProfileAsync(req);
            SuccessMessage = "Restaurant Profile and Tax configuration updated successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update restaurant profile.");
            ErrorMessage = $"Failed to save profile: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveAppConfigAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var req = new UpdateAppConfigRequest(
                KdsRefreshIntervalSeconds,
                DefaultOrderType,
                ReceiptHeaderMessage,
                ReceiptFooterMessage);

            await _settingsService.UpdateAppConfigAsync(req);
            SuccessMessage = "Application operational preferences saved.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save app configuration.");
            ErrorMessage = $"Failed to save preferences: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
