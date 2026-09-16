using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Kitchen.Interfaces;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Desktop.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly IDatabaseHealthService _databaseHealthService;
    private readonly IKitchenService _kitchenService;
    private readonly ILogger<DashboardViewModel> _logger;

    private string _databaseStatus = "Checking database health...";
    private bool _isDatabaseHealthy = true;
    private int _kitchenActiveCount;
    private int _kitchenPreparingCount;
    private int _kitchenReadyCount;

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

    public int KitchenActiveCount
    {
        get => _kitchenActiveCount;
        private set => SetProperty(ref _kitchenActiveCount, value);
    }

    public int KitchenPreparingCount
    {
        get => _kitchenPreparingCount;
        private set => SetProperty(ref _kitchenPreparingCount, value);
    }

    public int KitchenReadyCount
    {
        get => _kitchenReadyCount;
        private set => SetProperty(ref _kitchenReadyCount, value);
    }

    public DashboardViewModel(
        IDatabaseHealthService databaseHealthService,
        IKitchenService kitchenService,
        ILogger<DashboardViewModel> logger)
    {
        _databaseHealthService = databaseHealthService;
        _kitchenService = kitchenService;
        _logger = logger;

        _ = RefreshHealthAsync();
    }

    public async Task RefreshHealthAsync()
    {
        try
        {
            var health = await _databaseHealthService.CheckHealthAsync();
            if (health.IsHealthy)
            {
                IsDatabaseHealthy = true;
                DatabaseStatus = $"SQLite: Healthy ({health.ResponseTime?.TotalMilliseconds:F0} ms)";
            }
            else
            {
                IsDatabaseHealthy = false;
                DatabaseStatus = $"SQLite: {health.StatusMessage}";
            }

            try
            {
                var kitchenOrders = await _kitchenService.GetKitchenOrdersAsync();
                KitchenActiveCount = kitchenOrders.Count;
                KitchenPreparingCount = kitchenOrders.Count(o => o.Status == OrderStatus.Preparing);
                KitchenReadyCount = kitchenOrders.Count(o => o.Status == OrderStatus.Ready);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not fetch kitchen orders for dashboard summary.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing dashboard health.");
            IsDatabaseHealthy = false;
            DatabaseStatus = "SQLite: Connection Error";
        }
    }
}
