using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Kitchen.DTOs;
using RestaurantManagement.Application.Kitchen.Interfaces;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Desktop.ViewModels;

public class KitchenOrderItemViewModel : ViewModelBase
{
    public Guid ItemId { get; }
    public Guid? ProductId { get; }
    public string ProductName { get; }
    public int Quantity { get; }
    public string? Notes { get; }
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public KitchenOrderItemViewModel(KitchenOrderItemDto dto)
    {
        ItemId = dto.ItemId;
        ProductId = dto.ProductId;
        ProductName = dto.ProductNameSnapshot;
        Quantity = dto.Quantity;
        Notes = dto.Notes;
    }
}

public class KitchenOrderCardViewModel : ViewModelBase
{
    private readonly KitchenOrderDto _dto;
    private string _ageText = string.Empty;
    private bool _isUrgent;

    public Guid OrderId => _dto.OrderId;
    public string OrderNumber => _dto.OrderNumber;
    public OrderType OrderType => _dto.OrderType;
    public OrderStatus Status => _dto.Status;
    public Guid? RestaurantTableId => _dto.RestaurantTableId;
    public string? TableNumber => _dto.TableNumber;
    public string? Notes => _dto.Notes;
    public bool HasOrderNotes => !string.IsNullOrWhiteSpace(Notes);
    public DateTime CreatedAt => _dto.CreatedAt;
    public ObservableCollection<KitchenOrderItemViewModel> Items { get; }

    public bool IsDineIn => OrderType == OrderType.DineIn;
    public bool IsTakeaway => OrderType == OrderType.Takeaway;
    public bool IsDelivery => OrderType == OrderType.Delivery;

    public string OrderTypeBadge => OrderType switch
    {
        OrderType.DineIn => $"🪑 {TableNumber ?? "Table"}",
        OrderType.Takeaway => "🛍️ TAKEAWAY",
        OrderType.Delivery => "🛵 DELIVERY",
        _ => OrderType.ToString().ToUpperInvariant()
    };

    public string StatusText => Status switch
    {
        OrderStatus.Confirmed => "NEW / QUEUED",
        OrderStatus.Preparing => "PREPARING",
        OrderStatus.Ready => "READY FOR PICKUP",
        OrderStatus.Served => "SERVED",
        _ => Status.ToString().ToUpperInvariant()
    };

    public bool CanStartPreparation => Status == OrderStatus.Confirmed;
    public bool CanMarkReady => Status == OrderStatus.Confirmed || Status == OrderStatus.Preparing;
    public bool CanMarkServed => Status == OrderStatus.Confirmed || Status == OrderStatus.Preparing || Status == OrderStatus.Ready;

    public string AgeText
    {
        get => _ageText;
        private set => SetProperty(ref _ageText, value);
    }

    public bool IsUrgent
    {
        get => _isUrgent;
        private set => SetProperty(ref _isUrgent, value);
    }

    public KitchenOrderCardViewModel(KitchenOrderDto dto, DateTime currentUtc)
    {
        _dto = dto;
        Items = new ObservableCollection<KitchenOrderItemViewModel>(
            dto.Items.Select(i => new KitchenOrderItemViewModel(i)));

        UpdateAge(currentUtc);
    }

    public void UpdateAge(DateTime currentUtc)
    {
        var elapsed = currentUtc - CreatedAt;
        if (elapsed.TotalSeconds < 60)
        {
            AgeText = "Just now";
            IsUrgent = false;
        }
        else if (elapsed.TotalMinutes < 60)
        {
            var mins = (int)elapsed.TotalMinutes;
            AgeText = $"{mins} min ago";
            IsUrgent = mins >= 15;
        }
        else
        {
            var hours = (int)elapsed.TotalHours;
            var mins = elapsed.Minutes;
            AgeText = $"{hours}h {mins}m ago";
            IsUrgent = true;
        }
    }
}

public class KitchenViewModel : ViewModelBase
{
    private readonly IKitchenService _kitchenService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<KitchenViewModel> _logger;

    private readonly DispatcherTimer _autoRefreshTimer;
    private readonly DispatcherTimer _ageUpdateTimer;

    private readonly List<KitchenOrderCardViewModel> _allOrders = new();
    public ObservableCollection<KitchenOrderCardViewModel> FilteredOrders { get; } = new();

    private string _selectedFilter = "All";
    private int _totalActiveCount;
    private int _confirmedCount;
    private int _preparingCount;
    private int _readyCount;
    private string _lastRefreshedText = "Not refreshed";
    private bool _isLoading;
    private string? _statusMessage;
    private string? _errorMessage;

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public int TotalActiveCount
    {
        get => _totalActiveCount;
        private set
        {
            if (SetProperty(ref _totalActiveCount, value))
            {
                OnPropertyChanged(nameof(HasOrders));
                OnPropertyChanged(nameof(HasNoOrders));
            }
        }
    }

    public int ConfirmedCount
    {
        get => _confirmedCount;
        private set => SetProperty(ref _confirmedCount, value);
    }

    public int PreparingCount
    {
        get => _preparingCount;
        private set => SetProperty(ref _preparingCount, value);
    }

    public int ReadyCount
    {
        get => _readyCount;
        private set => SetProperty(ref _readyCount, value);
    }

    public bool HasOrders => TotalActiveCount > 0;
    public bool HasNoOrders => TotalActiveCount == 0;

    public string LastRefreshedText
    {
        get => _lastRefreshedText;
        private set => SetProperty(ref _lastRefreshedText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ICommand RefreshOrdersCommand { get; }
    public ICommand SetFilterCommand { get; }
    public ICommand StartPreparationCommand { get; }
    public ICommand MarkReadyCommand { get; }
    public ICommand MarkServedCommand { get; }

    public KitchenViewModel(
        IKitchenService kitchenService,
        IDateTimeProvider dateTimeProvider,
        ILogger<KitchenViewModel> logger)
    {
        _kitchenService = kitchenService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;

        RefreshOrdersCommand = new RelayCommand(async _ => await LoadKitchenOrdersAsync());
        SetFilterCommand = new RelayCommand<string>(filter =>
        {
            if (!string.IsNullOrWhiteSpace(filter))
            {
                SelectedFilter = filter;
            }
        });

        StartPreparationCommand = new RelayCommand<KitchenOrderCardViewModel>(async card =>
        {
            if (card != null)
            {
                await ExecuteStatusTransitionAsync(card.OrderId, _kitchenService.StartPreparationAsync, "Preparation started.");
            }
        });

        MarkReadyCommand = new RelayCommand<KitchenOrderCardViewModel>(async card =>
        {
            if (card != null)
            {
                await ExecuteStatusTransitionAsync(card.OrderId, _kitchenService.MarkOrderReadyAsync, "Order marked as Ready for service.");
            }
        });

        MarkServedCommand = new RelayCommand<KitchenOrderCardViewModel>(async card =>
        {
            if (card != null)
            {
                await ExecuteStatusTransitionAsync(card.OrderId, _kitchenService.MarkOrderServedAsync, "Order marked as Served.");
            }
        });

        // 5-second automatic polling timer for SQLite database changes
        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autoRefreshTimer.Tick += async (s, e) => await LoadKitchenOrdersSilentlyAsync();
        _autoRefreshTimer.Start();

        // 1-second elapsed age updater
        _ageUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _ageUpdateTimer.Tick += (s, e) => UpdateAges();
        _ageUpdateTimer.Start();

        _ = LoadKitchenOrdersAsync();
    }

    public async Task LoadKitchenOrdersAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            await FetchAndPopulateOrdersAsync();
            LastRefreshedText = _dateTimeProvider.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load kitchen display orders.");
            ErrorMessage = $"Failed to fetch kitchen orders: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadKitchenOrdersSilentlyAsync()
    {
        try
        {
            await FetchAndPopulateOrdersAsync();
            LastRefreshedText = _dateTimeProvider.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Silent kitchen refresh encountered an error.");
        }
    }

    private async Task FetchAndPopulateOrdersAsync()
    {
        var dtos = await _kitchenService.GetKitchenOrdersAsync();
        var now = _dateTimeProvider.UtcNow;

        _allOrders.Clear();
        foreach (var dto in dtos)
        {
            _allOrders.Add(new KitchenOrderCardViewModel(dto, now));
        }

        TotalActiveCount = _allOrders.Count;
        ConfirmedCount = _allOrders.Count(o => o.Status == OrderStatus.Confirmed);
        PreparingCount = _allOrders.Count(o => o.Status == OrderStatus.Preparing);
        ReadyCount = _allOrders.Count(o => o.Status == OrderStatus.Ready);

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredOrders.Clear();
        IEnumerable<KitchenOrderCardViewModel> filtered = SelectedFilter switch
        {
            "Confirmed" => _allOrders.Where(o => o.Status == OrderStatus.Confirmed),
            "Preparing" => _allOrders.Where(o => o.Status == OrderStatus.Preparing),
            "Ready" => _allOrders.Where(o => o.Status == OrderStatus.Ready),
            _ => _allOrders
        };

        foreach (var order in filtered)
        {
            FilteredOrders.Add(order);
        }
    }

    private void UpdateAges()
    {
        var now = _dateTimeProvider.UtcNow;
        foreach (var order in _allOrders)
        {
            order.UpdateAge(now);
        }
    }

    private async Task ExecuteStatusTransitionAsync(
        Guid orderId,
        Func<Guid, CancellationToken, Task<KitchenOrderDto>> transitionAction,
        string successMessage)
    {
        try
        {
            ErrorMessage = null;
            StatusMessage = null;

            await transitionAction(orderId, CancellationToken.None);
            StatusMessage = successMessage;

            await LoadKitchenOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transition kitchen order status.");
            ErrorMessage = ex.Message;
        }
    }
}
