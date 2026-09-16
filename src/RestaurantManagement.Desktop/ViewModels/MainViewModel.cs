using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Desktop.Services;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Desktop.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IAppInfoService _appInfoService;
    private readonly INavigationService _navigationService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDatabaseHealthService _databaseHealthService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly DispatcherTimer _clockTimer;

    private readonly DashboardViewModel _dashboardViewModel;
    private readonly OrdersViewModel _ordersViewModel;
    private readonly KitchenViewModel _kitchenViewModel;
    private readonly MenuViewModel _menuViewModel;
    private readonly TablesViewModel _tablesViewModel;
    private readonly EmployeesViewModel _employeesViewModel;
    private readonly BillingViewModel _billingViewModel;
    private readonly InventoryViewModel _inventoryViewModel;
    private readonly ReportsViewModel _reportsViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly PlaceholderViewModel _placeholderViewModel;
    private readonly LoginViewModel _loginViewModel;

    private ViewModelBase _currentViewViewModel;
    private NavigationItemViewModel? _selectedNavigationItem;
    private string _currentViewTitle = "Dashboard";
    private string _currentViewDescription = "System overview, database status, and health metrics.";
    private string _currentTime;
    private string _systemStatus = "Ready • Database Connected";
    private string _databaseStatus = "Checking database health...";
    private bool _isDatabaseHealthy = true;

    public string WindowTitle => $"{AppName} v{AppVersion}";
    public string AppName { get; }
    public string AppVersion { get; }
    public string EnvironmentName { get; }
    public string Architecture { get; }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }
    public ObservableCollection<NavigationItemViewModel> VisibleNavigationItems { get; } = new();

    public bool IsAuthenticated => _currentUserService.IsAuthenticated;
    public string CurrentUserDisplayName => _currentUserService.DisplayName ?? "Guest";
    public string CurrentUserUsername => _currentUserService.Username ?? string.Empty;
    public EmployeeRole? CurrentUserRole => _currentUserService.Role;
    public string CurrentUserRoleName => _currentUserService.Role?.ToString() ?? "Anonymous";

    public ViewModelBase CurrentViewViewModel
    {
        get => _currentViewViewModel;
        set => SetProperty(ref _currentViewViewModel, value);
    }

    public LoginViewModel LoginViewModel => _loginViewModel;

    public NavigationItemViewModel? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (SetProperty(ref _selectedNavigationItem, value) && value != null)
            {
                foreach (var item in NavigationItems)
                {
                    item.IsSelected = (item == value);
                }

                CurrentViewTitle = value.Title;
                CurrentViewDescription = value.Description;
                _navigationService.NavigateTo(value);
                SwitchView(value);
            }
        }
    }

    public string CurrentViewTitle
    {
        get => _currentViewTitle;
        set => SetProperty(ref _currentViewTitle, value);
    }

    public string CurrentViewDescription
    {
        get => _currentViewDescription;
        set => SetProperty(ref _currentViewDescription, value);
    }

    public string CurrentTime
    {
        get => _currentTime;
        set => SetProperty(ref _currentTime, value);
    }

    public string SystemStatus
    {
        get => _systemStatus;
        set => SetProperty(ref _systemStatus, value);
    }

    public string DatabaseStatus
    {
        get => _databaseStatus;
        set => SetProperty(ref _databaseStatus, value);
    }

    public bool IsDatabaseHealthy
    {
        get => _isDatabaseHealthy;
        set => SetProperty(ref _isDatabaseHealthy, value);
    }

    public ICommand SelectNavigationItemCommand { get; }
    public ICommand LogoutCommand { get; }

    public MainViewModel(
        IAppInfoService appInfoService,
        INavigationService navigationService,
        IDateTimeProvider dateTimeProvider,
        IDatabaseHealthService databaseHealthService,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        IAuthenticationService authenticationService,
        DashboardViewModel dashboardViewModel,
        OrdersViewModel ordersViewModel,
        KitchenViewModel kitchenViewModel,
        MenuViewModel menuViewModel,
        TablesViewModel tablesViewModel,
        EmployeesViewModel employeesViewModel,
        BillingViewModel billingViewModel,
        InventoryViewModel inventoryViewModel,
        ReportsViewModel reportsViewModel,
        SettingsViewModel settingsViewModel,
        PlaceholderViewModel placeholderViewModel,
        LoginViewModel loginViewModel,
        ILogger<MainViewModel> logger)
    {
        _appInfoService = appInfoService;
        _navigationService = navigationService;
        _dateTimeProvider = dateTimeProvider;
        _databaseHealthService = databaseHealthService;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _authenticationService = authenticationService;
        _dashboardViewModel = dashboardViewModel;
        _ordersViewModel = ordersViewModel;
        _kitchenViewModel = kitchenViewModel;
        _menuViewModel = menuViewModel;
        _tablesViewModel = tablesViewModel;
        _employeesViewModel = employeesViewModel;
        _billingViewModel = billingViewModel;
        _inventoryViewModel = inventoryViewModel;
        _reportsViewModel = reportsViewModel;
        _settingsViewModel = settingsViewModel;
        _placeholderViewModel = placeholderViewModel;
        _loginViewModel = loginViewModel;
        _logger = logger;

        _currentViewViewModel = _dashboardViewModel;

        var info = _appInfoService.GetAppInfo();
        AppName = info.ApplicationName;
        AppVersion = info.Version;
        EnvironmentName = info.Environment;
        Architecture = info.Architecture;
        _currentTime = _dateTimeProvider.Now.ToString("yyyy-MM-dd HH:mm:ss");

        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new("Dashboard", "dashboard", "📊", "System overview, database status, and health metrics.", isSelected: true),
            new("Orders", "orders", "🛒", "Order creation, product catalog, table assignment, and POS terminal."),
            new("Kitchen", "kitchen", "🍳", "Kitchen Display System (KDS), ticket queue, and preparation workflow."),
            new("Menu", "menu", "🍽️", "Menu item catalog, categories, pricing, and availability management."),
            new("Tables", "tables", "🪑", "Restaurant floor layout, table seating capacity, and occupancy status."),
            new("Inventory", "inventory", "📦", "Stock levels, low-stock threshold alerts, and inventory restock/waste logging."),
            new("Billing", "billing", "💳", "Checkout invoice generation, cash tender calculation, and local payments."),
            new("Employees", "employees", "👥", "Staff scheduling, role permissions, and employee account administration."),
            new("Reports", "reports", "📈", "Daily sales summaries, payment method distribution, and product sales analytics."),
            new("Settings", "settings", "⚙️", "Restaurant profile, default tax rates, SQLite diagnostics, and preferences.")
        };

        _selectedNavigationItem = NavigationItems[0];

        SelectNavigationItemCommand = new RelayCommand<NavigationItemViewModel>(item =>
        {
            if (item != null)
            {
                SelectedNavigationItem = item;
            }
        });

        LogoutCommand = new RelayCommand(async _ => await ExecuteLogoutAsync());

        // Wire login success callback
        _loginViewModel.LoginSucceeded += OnLoginSucceeded;

        // Listen for session changes
        _currentUserService.CurrentUserChanged += OnCurrentUserChanged;

        // Initialize clock timer for status bar
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) =>
        {
            CurrentTime = _dateTimeProvider.Now.ToString("yyyy-MM-dd HH:mm:ss");
        };
        _clockTimer.Start();

        // Perform initial database health check and check login setup
        _ = CheckDatabaseHealthAsync();
        _ = _loginViewModel.CheckInitialSetupAsync();

        UpdateNavigationPermissions();
        _logger.LogInformation("MainViewModel initialized for {AppName} v{AppVersion}", AppName, AppVersion);
    }

    private void OnCurrentUserChanged()
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(CurrentUserDisplayName));
        OnPropertyChanged(nameof(CurrentUserUsername));
        OnPropertyChanged(nameof(CurrentUserRole));
        OnPropertyChanged(nameof(CurrentUserRoleName));

        UpdateNavigationPermissions();
    }

    private void OnLoginSucceeded()
    {
        OnCurrentUserChanged();
        SelectedNavigationItem = NavigationItems[0];
        SwitchView(NavigationItems[0]);
    }

    private async Task ExecuteLogoutAsync()
    {
        try
        {
            await _authenticationService.LogoutAsync();
            _loginViewModel.ResetForm();
            await _loginViewModel.CheckInitialSetupAsync();
            _logger.LogInformation("User signed out successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user logout.");
        }
    }

    private void UpdateNavigationPermissions()
    {
        VisibleNavigationItems.Clear();

        if (!IsAuthenticated)
        {
            return;
        }

        foreach (var item in NavigationItems)
        {
            if (_authorizationService.CanAccessFeature(CurrentUserRole, item.Tag))
            {
                VisibleNavigationItems.Add(item);
            }
        }

        // If current selected item is not accessible, select the first visible
        if (_selectedNavigationItem != null && !VisibleNavigationItems.Contains(_selectedNavigationItem))
        {
            var first = VisibleNavigationItems.FirstOrDefault();
            if (first != null)
            {
                SelectedNavigationItem = first;
            }
        }
    }

    private void SwitchView(NavigationItemViewModel item)
    {
        switch (item.Tag.ToLowerInvariant())
        {
            case "dashboard":
                CurrentViewViewModel = _dashboardViewModel;
                _ = _dashboardViewModel.RefreshHealthAsync();
                break;
            case "orders":
                CurrentViewViewModel = _ordersViewModel;
                _ = _ordersViewModel.LoadInitialDataAsync();
                break;
            case "kitchen":
                CurrentViewViewModel = _kitchenViewModel;
                _ = _kitchenViewModel.LoadKitchenOrdersAsync();
                break;
            case "menu":
                CurrentViewViewModel = _menuViewModel;
                _ = _menuViewModel.LoadDataAsync();
                break;
            case "tables":
                CurrentViewViewModel = _tablesViewModel;
                _ = _tablesViewModel.LoadTablesAsync();
                break;
            case "employees":
                CurrentViewViewModel = _employeesViewModel;
                _ = _employeesViewModel.LoadEmployeesAsync();
                break;
            case "billing":
                CurrentViewViewModel = _billingViewModel;
                _ = _billingViewModel.LoadBillingOrdersAsync();
                break;
            case "inventory":
                CurrentViewViewModel = _inventoryViewModel;
                _ = _inventoryViewModel.LoadInitialDataAsync();
                break;
            case "reports":
                CurrentViewViewModel = _reportsViewModel;
                _ = _reportsViewModel.GenerateReportAsync();
                break;
            case "settings":
                CurrentViewViewModel = _settingsViewModel;
                _ = _settingsViewModel.LoadAllSettingsAsync();
                break;
            default:
                _placeholderViewModel.SetContext(item.Title, item.IconGlyph, item.Description);
                CurrentViewViewModel = _placeholderViewModel;
                break;
        }
    }

    private async Task CheckDatabaseHealthAsync()
    {
        try
        {
            var health = await _databaseHealthService.CheckHealthAsync();
            if (health.IsHealthy)
            {
                IsDatabaseHealthy = true;
                DatabaseStatus = $"SQLite: Healthy ({health.ResponseTime?.TotalMilliseconds:F0} ms)";
                SystemStatus = "Ready • All Sections Operational";
            }
            else
            {
                IsDatabaseHealthy = false;
                DatabaseStatus = $"SQLite: {health.StatusMessage}";
                SystemStatus = "Warning • Database Degraded";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking database health from MainViewModel.");
            IsDatabaseHealthy = false;
            DatabaseStatus = "SQLite: Connection Error";
            SystemStatus = "Error • Database Offline";
        }
    }
}
