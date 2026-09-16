using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Menu.DTOs;
using RestaurantManagement.Application.Menu.Interfaces;
using RestaurantManagement.Application.Orders.DTOs;
using RestaurantManagement.Application.Orders.Interfaces;
using RestaurantManagement.Application.Tables.DTOs;
using RestaurantManagement.Application.Tables.Interfaces;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Desktop.ViewModels;

public class TableItemViewModel : ViewModelBase
{
    public RestaurantTableDto Table { get; }
    public Guid Id => Table.Id;
    public string TableNumber => Table.TableNumber;
    public int Capacity => Table.Capacity;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isOccupied;
    public bool IsOccupied
    {
        get => _isOccupied;
        set => SetProperty(ref _isOccupied, value);
    }

    public string StatusText => IsOccupied ? "Occupied" : "Available";

    public TableItemViewModel(RestaurantTableDto table, bool isOccupied, bool isSelected)
    {
        Table = table;
        _isOccupied = isOccupied;
        _isSelected = isSelected;
    }
}

public class CategoryItemViewModel : ViewModelBase
{
    public CategoryDto? Category { get; }
    public string Name => Category?.Name ?? "All Items";
    public Guid? Id => Category?.Id;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public CategoryItemViewModel(CategoryDto? category, bool isSelected)
    {
        Category = category;
        _isSelected = isSelected;
    }
}

public class OrdersViewModel : ViewModelBase
{
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly ITableService _tableService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<OrdersViewModel> _logger;

    private List<ProductDto> _allProducts = new();
    private CategoryItemViewModel? _selectedCategoryItem;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string? _statusMessage;
    private string? _errorMessage;

    // View Mode: true = Setup Screen (Select Order Type / Table), false = POS Menu & Ticket Terminal
    private bool _isOrderSetupActive = true;

    // Active Order State
    private OrderDto? _activeOrder;
    private bool _isItemNotesModalOpen;

    // Order Setup State
    private OrderType _setupOrderType = OrderType.DineIn;
    private TableItemViewModel? _setupSelectedTableItem;
    private string? _setupNotes;
    private string? _setupError;

    // Item Notes Modal State
    private OrderItemDto? _editingOrderItem;
    private string? _editingItemNotesText;

    // Collections
    public ObservableCollection<CategoryItemViewModel> CategoryItems { get; } = new();
    public ObservableCollection<ProductDto> FilteredProducts { get; } = new();
    public ObservableCollection<TableItemViewModel> TableItems { get; } = new();
    public ObservableCollection<OrderDto> OpenOrders { get; } = new();
    public ObservableCollection<OrderItemDto> ActiveOrderItems { get; } = new();
    public IReadOnlyDictionary<Guid, bool> TableOccupancyMap { get; private set; } = new Dictionary<Guid, bool>();

    // View Mode
    public bool IsOrderSetupActive
    {
        get => _isOrderSetupActive;
        set
        {
            if (SetProperty(ref _isOrderSetupActive, value))
            {
                OnPropertyChanged(nameof(IsMenuCatalogActive));
            }
        }
    }

    public bool IsMenuCatalogActive => !IsOrderSetupActive;

    // Setup Screen Properties
    public OrderType SetupOrderType
    {
        get => _setupOrderType;
        set
        {
            if (SetProperty(ref _setupOrderType, value))
            {
                OnPropertyChanged(nameof(IsSetupDineIn));
                OnPropertyChanged(nameof(IsSetupTakeaway));
                OnPropertyChanged(nameof(IsSetupDelivery));
            }
        }
    }

    public bool IsSetupDineIn => SetupOrderType == OrderType.DineIn;
    public bool IsSetupTakeaway => SetupOrderType == OrderType.Takeaway;
    public bool IsSetupDelivery => SetupOrderType == OrderType.Delivery;

    public TableItemViewModel? SetupSelectedTableItem
    {
        get => _setupSelectedTableItem;
        set
        {
            if (SetProperty(ref _setupSelectedTableItem, value))
            {
                foreach (var item in TableItems)
                {
                    item.IsSelected = (item == value);
                }
                OnPropertyChanged(nameof(SetupSelectedTableName));
            }
        }
    }

    public string SetupSelectedTableName => SetupSelectedTableItem != null ? $"Table {SetupSelectedTableItem.TableNumber}" : "None Selected";

    public string? SetupNotes
    {
        get => _setupNotes;
        set => SetProperty(ref _setupNotes, value);
    }

    public string? SetupError
    {
        get => _setupError;
        set
        {
            if (SetProperty(ref _setupError, value))
            {
                OnPropertyChanged(nameof(HasSetupError));
            }
        }
    }

    public bool HasSetupError => !string.IsNullOrWhiteSpace(SetupError);

    // Active Order Properties
    public OrderDto? ActiveOrder
    {
        get => _activeOrder;
        private set
        {
            if (SetProperty(ref _activeOrder, value))
            {
                OnPropertyChanged(nameof(HasActiveOrder));
                OnPropertyChanged(nameof(ActiveOrderNumber));
                OnPropertyChanged(nameof(ActiveOrderStatus));
                OnPropertyChanged(nameof(ActiveOrderType));
                OnPropertyChanged(nameof(ActiveOrderTableName));
                OnPropertyChanged(nameof(ActiveOrderSubtotal));
                OnPropertyChanged(nameof(ActiveOrderDiscount));
                OnPropertyChanged(nameof(ActiveOrderTax));
                OnPropertyChanged(nameof(ActiveOrderTotal));
                OnPropertyChanged(nameof(ActiveOrderNotes));
                OnPropertyChanged(nameof(CanModifyActiveOrder));
                OnPropertyChanged(nameof(CanConfirmActiveOrder));
                OnPropertyChanged(nameof(CanCompleteActiveOrder));
                OnPropertyChanged(nameof(CanCancelActiveOrder));

                ActiveOrderItems.Clear();
                if (value != null)
                {
                    foreach (var item in value.Items)
                    {
                        ActiveOrderItems.Add(item);
                    }
                }
                OnPropertyChanged(nameof(HasActiveOrderItems));
                OnPropertyChanged(nameof(HasNoActiveOrderItems));
            }
        }
    }

    public bool HasActiveOrder => _activeOrder != null;
    public string ActiveOrderNumber => _activeOrder?.OrderNumber ?? "No Active Order";
    public OrderStatus? ActiveOrderStatus => _activeOrder?.Status;
    public OrderType? ActiveOrderType => _activeOrder?.OrderType;
    public string ActiveOrderTableName => _activeOrder?.TableNumber != null ? $"Table {_activeOrder.TableNumber}" : (_activeOrder?.OrderType.ToString() ?? "-");
    public decimal ActiveOrderSubtotal => _activeOrder?.Subtotal ?? 0m;
    public decimal ActiveOrderDiscount => _activeOrder?.DiscountAmount ?? 0m;
    public decimal ActiveOrderTax => _activeOrder?.TaxAmount ?? 0m;
    public decimal ActiveOrderTotal => _activeOrder?.TotalAmount ?? 0m;
    public string? ActiveOrderNotes => _activeOrder?.Notes;
    public bool CanModifyActiveOrder => _activeOrder != null && (_activeOrder.Status == OrderStatus.Draft || _activeOrder.Status == OrderStatus.Confirmed);
    public bool CanConfirmActiveOrder => _activeOrder != null && _activeOrder.Status == OrderStatus.Draft && _activeOrder.Items.Count > 0;
    public bool CanCompleteActiveOrder => _activeOrder != null && (_activeOrder.Status == OrderStatus.Draft || _activeOrder.Status == OrderStatus.Confirmed || _activeOrder.Status == OrderStatus.Served || _activeOrder.Status == OrderStatus.Ready);
    public bool CanCancelActiveOrder => _activeOrder != null && _activeOrder.Status != OrderStatus.Completed && _activeOrder.Status != OrderStatus.Cancelled;

    public bool HasActiveOrderItems => ActiveOrderItems.Count > 0;
    public bool HasNoActiveOrderItems => ActiveOrderItems.Count == 0;
    public bool HasOpenOrders => OpenOrders.Count > 0;
    public bool HasNoOpenOrders => OpenOrders.Count == 0;

    // Filter & Search
    public CategoryItemViewModel? SelectedCategoryItem
    {
        get => _selectedCategoryItem;
        set
        {
            if (SetProperty(ref _selectedCategoryItem, value))
            {
                foreach (var item in CategoryItems)
                {
                    item.IsSelected = (item == value);
                }
                ApplyProductFilter();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyProductFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
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

    // Item Notes Properties
    public bool IsItemNotesModalOpen
    {
        get => _isItemNotesModalOpen;
        set => SetProperty(ref _isItemNotesModalOpen, value);
    }

    public OrderItemDto? EditingOrderItem
    {
        get => _editingOrderItem;
        set => SetProperty(ref _editingOrderItem, value);
    }

    public string? EditingItemNotesText
    {
        get => _editingItemNotesText;
        set => SetProperty(ref _editingItemNotesText, value);
    }

    // Commands
    public ICommand SelectCategoryCommand { get; }
    public ICommand AddProductToOrderCommand { get; }
    public ICommand IncrementQuantityCommand { get; }
    public ICommand DecrementQuantityCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand OpenItemNotesCommand { get; }
    public ICommand SaveItemNotesCommand { get; }
    public ICommand CloseItemNotesCommand { get; }

    public ICommand SelectOrderTypeCommand { get; }
    public ICommand SelectTableCommand { get; }
    public ICommand StartOrderFromSetupCommand { get; }
    public ICommand OpenSetupScreenCommand { get; }

    public ICommand SelectOpenOrderCommand { get; }
    public ICommand RefreshOpenOrdersCommand { get; }

    public ICommand ConfirmActiveOrderCommand { get; }
    public ICommand CompleteActiveOrderCommand { get; }
    public ICommand CancelActiveOrderCommand { get; }
    public ICommand ClearActiveOrderCommand { get; }
    public ICommand RefreshDataCommand { get; }

    public OrdersViewModel(
        IOrderService orderService,
        IProductService productService,
        ICategoryService categoryService,
        ITableService tableService,
        ICurrentUserService currentUserService,
        ILogger<OrdersViewModel> logger)
    {
        _orderService = orderService;
        _productService = productService;
        _categoryService = categoryService;
        _tableService = tableService;
        _currentUserService = currentUserService;
        _logger = logger;

        SelectCategoryCommand = new RelayCommand<CategoryItemViewModel>(cat => SelectedCategoryItem = cat);
        AddProductToOrderCommand = new RelayCommand<ProductDto>(async p => await ExecuteAddProductAsync(p));
        IncrementQuantityCommand = new RelayCommand<OrderItemDto>(async item => await ExecuteIncrementQuantityAsync(item));
        DecrementQuantityCommand = new RelayCommand<OrderItemDto>(async item => await ExecuteDecrementQuantityAsync(item));
        RemoveItemCommand = new RelayCommand<OrderItemDto>(async item => await ExecuteRemoveItemAsync(item));

        OpenItemNotesCommand = new RelayCommand<OrderItemDto>(ExecuteOpenItemNotes);
        SaveItemNotesCommand = new RelayCommand(async _ => await ExecuteSaveItemNotesAsync());
        CloseItemNotesCommand = new RelayCommand(_ => IsItemNotesModalOpen = false);

        SelectOrderTypeCommand = new RelayCommand<OrderType>(t =>
        {
            SetupOrderType = t;
            SetupError = null;
        });

        SelectTableCommand = new RelayCommand<TableItemViewModel>(t =>
        {
            SetupSelectedTableItem = t;
            SetupError = null;
        });

        StartOrderFromSetupCommand = new RelayCommand(async _ => await ExecuteStartOrderFromSetupAsync());
        OpenSetupScreenCommand = new RelayCommand(_ => ExecuteOpenSetupScreen());

        SelectOpenOrderCommand = new RelayCommand<OrderDto>(async o => await ExecuteSelectOpenOrderAsync(o));
        RefreshOpenOrdersCommand = new RelayCommand(async _ => await LoadOpenOrdersAsync());

        ConfirmActiveOrderCommand = new RelayCommand(async _ => await ExecuteConfirmActiveOrderAsync(), _ => CanConfirmActiveOrder);
        CompleteActiveOrderCommand = new RelayCommand(async _ => await ExecuteCompleteActiveOrderAsync(), _ => CanCompleteActiveOrder);
        CancelActiveOrderCommand = new RelayCommand(async _ => await ExecuteCancelActiveOrderAsync(), _ => CanCancelActiveOrder);
        ClearActiveOrderCommand = new RelayCommand(_ =>
        {
            ActiveOrder = null;
            IsOrderSetupActive = true;
        });
        RefreshDataCommand = new RelayCommand(async _ => await LoadInitialDataAsync());
    }

    public async Task LoadInitialDataAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusMessage = "Loading POS catalog...";

            // Load Categories
            var categories = await _categoryService.GetCategoriesAsync(includeInactive: false);
            CategoryItems.Clear();
            var allCategoriesItem = new CategoryItemViewModel(null, isSelected: true);
            CategoryItems.Add(allCategoriesItem);
            _selectedCategoryItem = allCategoriesItem;

            foreach (var category in categories)
            {
                CategoryItems.Add(new CategoryItemViewModel(category, isSelected: false));
            }

            // Load Products (Only active and in-stock / available products appear in POS ordering menu)
            var products = await _productService.GetProductsAsync(includeInactive: false);
            _allProducts = products.Where(p => p.IsAvailable && p.IsActive).ToList();
            ApplyProductFilter();

            // Load Tables & Occupancy
            TableOccupancyMap = await _orderService.GetTableOccupancyMapAsync();
            var tables = await _tableService.GetActiveTablesAsync();
            TableItems.Clear();
            TableItemViewModel? firstAvailable = null;

            foreach (var table in tables)
            {
                var isOccupied = TableOccupancyMap.GetValueOrDefault(table.Id, false);
                var item = new TableItemViewModel(table, isOccupied, isSelected: false);
                TableItems.Add(item);

                if (!isOccupied && firstAvailable == null)
                {
                    firstAvailable = item;
                }
            }

            if (firstAvailable != null && SetupSelectedTableItem == null)
            {
                SetupSelectedTableItem = firstAvailable;
            }

            await LoadOpenOrdersAsync();

            // Default to setup screen if no order is active
            if (ActiveOrder == null)
            {
                IsOrderSetupActive = true;
            }

            StatusMessage = "POS Ready.";
            _logger.LogInformation("Loaded {ProductCount} products, {CategoryCount} categories, and {OrderCount} open orders.",
                _allProducts.Count, CategoryItems.Count, OpenOrders.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load POS data.");
            ErrorMessage = "Failed to load product catalog and orders.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadOpenOrdersAsync()
    {
        try
        {
            var openOrders = await _orderService.GetOrdersAsync(onlyOpen: true);
            OpenOrders.Clear();
            foreach (var order in openOrders)
            {
                OpenOrders.Add(order);
            }

            TableOccupancyMap = await _orderService.GetTableOccupancyMapAsync();
            foreach (var tableItem in TableItems)
            {
                tableItem.IsOccupied = TableOccupancyMap.GetValueOrDefault(tableItem.Id, false);
            }

            OnPropertyChanged(nameof(HasOpenOrders));
            OnPropertyChanged(nameof(HasNoOpenOrders));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load open orders.");
        }
    }

    private void ApplyProductFilter()
    {
        var query = _allProducts.Where(p => p.IsAvailable && p.IsActive);

        if (SelectedCategoryItem?.Category != null)
        {
            query = query.Where(p => p.CategoryId == SelectedCategoryItem.Category.Id);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (p.Description != null && p.Description.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        FilteredProducts.Clear();
        foreach (var product in query.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name))
        {
            FilteredProducts.Add(product);
        }
    }

    private void ExecuteOpenSetupScreen()
    {
        SetupError = null;
        SetupOrderType = OrderType.DineIn;
        SetupSelectedTableItem = TableItems.FirstOrDefault(t => !t.IsOccupied);
        SetupNotes = string.Empty;
        IsOrderSetupActive = true;
        _ = LoadOpenOrdersAsync();
    }

    private async Task ExecuteStartOrderFromSetupAsync()
    {
        try
        {
            SetupError = null;

            if (SetupOrderType == OrderType.DineIn && SetupSelectedTableItem == null)
            {
                SetupError = "Please select a dining table for Dine-In orders.";
                return;
            }

            var request = new CreateOrderRequest(
                SetupOrderType,
                SetupOrderType == OrderType.DineIn ? SetupSelectedTableItem?.Id : null,
                SetupNotes);

            var created = await _orderService.CreateOrderAsync(request);
            ActiveOrder = created;
            IsOrderSetupActive = false;
            await LoadOpenOrdersAsync();

            StatusMessage = $"Started order {created.OrderNumber} ({created.OrderType}).";
        }
        catch (ValidationException vex)
        {
            SetupError = vex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting order from setup.");
            SetupError = "Failed to start order: " + ex.Message;
        }
    }

    private async Task ExecuteAddProductAsync(ProductDto? product)
    {
        if (product == null) return;

        if (ActiveOrder == null || ActiveOrder.Status == OrderStatus.Completed || ActiveOrder.Status == OrderStatus.Cancelled)
        {
            IsOrderSetupActive = true;
            SetupError = "Please start a new order or select an open order first.";
            return;
        }

        try
        {
            ErrorMessage = null;
            var updatedOrder = await _orderService.AddItemToOrderAsync(ActiveOrder.Id, new AddOrderItemRequest(product.Id, 1));
            ActiveOrder = updatedOrder;
            await LoadOpenOrdersAsync();
            StatusMessage = $"Added '{product.Name}' to order {ActiveOrder.OrderNumber}.";
        }
        catch (ValidationException vex)
        {
            ErrorMessage = vex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding product {ProductName} to order.", product.Name);
            ErrorMessage = $"Failed to add '{product.Name}'.";
        }
    }

    private async Task ExecuteIncrementQuantityAsync(OrderItemDto? item)
    {
        if (item == null || ActiveOrder == null) return;

        try
        {
            ErrorMessage = null;
            var updated = await _orderService.UpdateOrderItemQuantityAsync(
                ActiveOrder.Id,
                new UpdateOrderItemQuantityRequest(item.Id, item.Quantity + 1));

            ActiveOrder = updated;
            await LoadOpenOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing item quantity.");
            ErrorMessage = "Failed to update item quantity.";
        }
    }

    private async Task ExecuteDecrementQuantityAsync(OrderItemDto? item)
    {
        if (item == null || ActiveOrder == null) return;

        try
        {
            ErrorMessage = null;
            if (item.Quantity > 1)
            {
                var updated = await _orderService.UpdateOrderItemQuantityAsync(
                    ActiveOrder.Id,
                    new UpdateOrderItemQuantityRequest(item.Id, item.Quantity - 1));

                ActiveOrder = updated;
            }
            else
            {
                var updated = await _orderService.RemoveOrderItemAsync(ActiveOrder.Id, item.Id);
                ActiveOrder = updated;
            }

            await LoadOpenOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrementing item quantity.");
            ErrorMessage = "Failed to update item quantity.";
        }
    }

    private async Task ExecuteRemoveItemAsync(OrderItemDto? item)
    {
        if (item == null || ActiveOrder == null) return;

        try
        {
            ErrorMessage = null;
            var updated = await _orderService.RemoveOrderItemAsync(ActiveOrder.Id, item.Id);
            ActiveOrder = updated;
            await LoadOpenOrdersAsync();
            StatusMessage = $"Removed item '{item.ProductNameSnapshot}'.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item from order.");
            ErrorMessage = "Failed to remove item.";
        }
    }

    private void ExecuteOpenItemNotes(OrderItemDto? item)
    {
        if (item == null) return;
        EditingOrderItem = item;
        EditingItemNotesText = item.Notes;
        IsItemNotesModalOpen = true;
    }

    private async Task ExecuteSaveItemNotesAsync()
    {
        if (EditingOrderItem == null || ActiveOrder == null) return;

        try
        {
            ErrorMessage = null;
            var updated = await _orderService.UpdateOrderItemNotesAsync(
                ActiveOrder.Id,
                new UpdateOrderItemNotesRequest(EditingOrderItem.Id, EditingItemNotesText));

            ActiveOrder = updated;
            IsItemNotesModalOpen = false;
            StatusMessage = "Item special instructions updated.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item notes.");
            ErrorMessage = "Failed to save item notes.";
        }
    }

    private async Task ExecuteSelectOpenOrderAsync(OrderDto? order)
    {
        if (order == null) return;

        try
        {
            ErrorMessage = null;
            var fullOrder = await _orderService.GetOrderByIdAsync(order.Id);
            if (fullOrder != null)
            {
                ActiveOrder = fullOrder;
                IsOrderSetupActive = false;
                StatusMessage = $"Loaded active order {fullOrder.OrderNumber}.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading order {OrderId}", order.Id);
            ErrorMessage = "Failed to load selected order.";
        }
    }

    private async Task ExecuteConfirmActiveOrderAsync()
    {
        if (ActiveOrder == null) return;

        try
        {
            ErrorMessage = null;
            var updated = await _orderService.ActivateOrderAsync(ActiveOrder.Id);
            ActiveOrder = updated;
            await LoadOpenOrdersAsync();
            StatusMessage = $"Order {updated.OrderNumber} confirmed and sent to preparation.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming order {OrderId}", ActiveOrder.Id);
            ErrorMessage = "Failed to confirm order: " + ex.Message;
        }
    }

    private async Task ExecuteCompleteActiveOrderAsync()
    {
        if (ActiveOrder == null) return;

        var orderId = ActiveOrder.Id;
        var orderNum = ActiveOrder.OrderNumber;

        try
        {
            ErrorMessage = null;
            var updated = await _orderService.CompleteOrderAsync(orderId);
            ActiveOrder = null;
            IsOrderSetupActive = true;
            await LoadOpenOrdersAsync();
            StatusMessage = $"Order {orderNum} successfully marked as Completed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing order {OrderId}", orderId);
            ErrorMessage = "Failed to complete order: " + ex.Message;
        }
    }

    private async Task ExecuteCancelActiveOrderAsync()
    {
        if (ActiveOrder == null) return;

        var orderId = ActiveOrder.Id;
        var orderNum = ActiveOrder.OrderNumber;

        try
        {
            ErrorMessage = null;
            await _orderService.CancelOrderAsync(orderId, "Cancelled by user");
            ActiveOrder = null;
            IsOrderSetupActive = true;
            await LoadOpenOrdersAsync();
            StatusMessage = $"Order {orderNum} cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
            ErrorMessage = "Failed to cancel order: " + ex.Message;
        }
    }
}
