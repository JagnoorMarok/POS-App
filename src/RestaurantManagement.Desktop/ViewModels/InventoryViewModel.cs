using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Inventory.DTOs;
using RestaurantManagement.Application.Inventory.Interfaces;
using RestaurantManagement.Application.Menu.Interfaces;

namespace RestaurantManagement.Desktop.ViewModels;

public class InventoryViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ICategoryService _categoryService;
    private readonly ILogger<InventoryViewModel> _logger;

    private bool _isLoading;
    private string? _errorMessage;
    private string? _successMessage;
    private string _searchText = string.Empty;
    private string _selectedFilter = "All"; // All, LowStock, OutOfStock, InStock

    private int _totalItems;
    private int _inStockCount;
    private int _lowStockCount;
    private int _outOfStockCount;

    private InventoryItemViewModel? _selectedItem;
    private int _adjustmentDelta = 10;
    private int _newExactStock = 50;
    private string _adjustmentReason = "Restock Delivery";
    private bool _isAdjustDialogOpen;

    public ObservableCollection<InventoryItemViewModel> InventoryItems { get; } = new();
    public ObservableCollection<CategoryFilterItemViewModel> CategoryFilters { get; } = new();
    private Guid? _selectedCategoryId;

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

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = LoadInventoryAsync();
            }
        }
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                _ = LoadInventoryAsync();
            }
        }
    }

    public int TotalItems
    {
        get => _totalItems;
        set => SetProperty(ref _totalItems, value);
    }

    public int InStockCount
    {
        get => _inStockCount;
        set => SetProperty(ref _inStockCount, value);
    }

    public int LowStockCount
    {
        get => _lowStockCount;
        set => SetProperty(ref _lowStockCount, value);
    }

    public int OutOfStockCount
    {
        get => _outOfStockCount;
        set => SetProperty(ref _outOfStockCount, value);
    }

    public InventoryItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value) && value != null)
            {
                NewExactStock = value.CurrentStock;
            }
        }
    }

    public int AdjustmentDelta
    {
        get => _adjustmentDelta;
        set => SetProperty(ref _adjustmentDelta, value);
    }

    public int NewExactStock
    {
        get => _newExactStock;
        set => SetProperty(ref _newExactStock, value);
    }

    public string AdjustmentReason
    {
        get => _adjustmentReason;
        set => SetProperty(ref _adjustmentReason, value);
    }

    public bool IsAdjustDialogOpen
    {
        get => _isAdjustDialogOpen;
        set => SetProperty(ref _isAdjustDialogOpen, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand SetFilterCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand OpenAdjustDialogCommand { get; }
    public ICommand CloseAdjustDialogCommand { get; }
    public ICommand QuickRestockCommand { get; }
    public ICommand QuickWasteCommand { get; }
    public ICommand ApplyAdjustmentDeltaCommand { get; }
    public ICommand SaveExactStockCommand { get; }

    public InventoryViewModel(
        IInventoryService inventoryService,
        ICategoryService categoryService,
        ILogger<InventoryViewModel> logger)
    {
        _inventoryService = inventoryService;
        _categoryService = categoryService;
        _logger = logger;

        RefreshCommand = new RelayCommand(async _ => await LoadInventoryAsync());
        SetFilterCommand = new RelayCommand<string>(filter =>
        {
            if (!string.IsNullOrEmpty(filter))
            {
                SelectedFilter = filter;
            }
        });

        SelectCategoryCommand = new RelayCommand<CategoryFilterItemViewModel>(cat =>
        {
            if (cat != null)
            {
                foreach (var c in CategoryFilters) c.IsSelected = (c == cat);
                _selectedCategoryId = cat.CategoryId;
                _ = LoadInventoryAsync();
            }
        });

        OpenAdjustDialogCommand = new RelayCommand<InventoryItemViewModel>(item =>
        {
            if (item != null)
            {
                SelectedItem = item;
                IsAdjustDialogOpen = true;
            }
        });

        CloseAdjustDialogCommand = new RelayCommand(_ => IsAdjustDialogOpen = false);

        QuickRestockCommand = new RelayCommand<object>(async param =>
        {
            if (SelectedItem != null && param != null && int.TryParse(param.ToString(), out var qty))
            {
                await AdjustStockInternalAsync(SelectedItem.ProductId, qty, "Quick Restock");
            }
        });

        QuickWasteCommand = new RelayCommand<object>(async param =>
        {
            if (SelectedItem != null && param != null && int.TryParse(param.ToString(), out var qty))
            {
                await AdjustStockInternalAsync(SelectedItem.ProductId, -qty, "Wastage / Spoilage");
            }
        });

        ApplyAdjustmentDeltaCommand = new RelayCommand(async _ =>
        {
            if (SelectedItem != null && AdjustmentDelta != 0)
            {
                await AdjustStockInternalAsync(SelectedItem.ProductId, AdjustmentDelta, AdjustmentReason);
                IsAdjustDialogOpen = false;
            }
        });

        SaveExactStockCommand = new RelayCommand(async _ =>
        {
            if (SelectedItem != null && NewExactStock >= 0)
            {
                await SetStockInternalAsync(SelectedItem.ProductId, NewExactStock, AdjustmentReason);
                IsAdjustDialogOpen = false;
            }
        });
    }

    public async Task LoadInitialDataAsync()
    {
        await LoadCategoriesAsync();
        await LoadInventoryAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _categoryService.GetCategoriesAsync();
            CategoryFilters.Clear();
            CategoryFilters.Add(new CategoryFilterItemViewModel(null, "All Categories", true));
            foreach (var c in categories)
            {
                CategoryFilters.Add(new CategoryFilterItemViewModel(c.Id, c.Name, false));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading categories for inventory filter.");
        }
    }

    public async Task LoadInventoryAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            bool lowStockOnly = SelectedFilter == "LowStock";
            var summary = await _inventoryService.GetInventorySummaryAsync(
                searchQuery: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                categoryId: _selectedCategoryId,
                lowStockOnly: lowStockOnly);

            TotalItems = summary.TotalItems;
            InStockCount = summary.InStockCount;
            LowStockCount = summary.LowStockCount;
            OutOfStockCount = summary.OutOfStockCount;

            var filteredItems = summary.Items.AsEnumerable();
            if (SelectedFilter == "OutOfStock")
            {
                filteredItems = filteredItems.Where(i => i.StockStatus == "Out of Stock");
            }
            else if (SelectedFilter == "InStock")
            {
                filteredItems = filteredItems.Where(i => i.StockStatus == "In Stock");
            }

            InventoryItems.Clear();
            foreach (var item in filteredItems)
            {
                InventoryItems.Add(new InventoryItemViewModel(item));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load inventory items.");
            ErrorMessage = "Failed to load inventory. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AdjustStockInternalAsync(Guid productId, int delta, string reason)
    {
        try
        {
            ErrorMessage = null;
            var updated = await _inventoryService.AdjustStockAsync(new AdjustStockRequest(productId, delta, reason));
            SuccessMessage = $"Updated {updated.ProductName}: Stock is now {updated.CurrentStock}.";
            await LoadInventoryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting stock for product {ProductId}", productId);
            ErrorMessage = $"Failed to adjust stock: {ex.Message}";
        }
    }

    private async Task SetStockInternalAsync(Guid productId, int newStock, string reason)
    {
        try
        {
            ErrorMessage = null;
            var updated = await _inventoryService.UpdateStockAsync(new UpdateStockRequest(productId, newStock, Reason: reason));
            SuccessMessage = $"Set {updated.ProductName} stock to {updated.CurrentStock}.";
            await LoadInventoryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting stock for product {ProductId}", productId);
            ErrorMessage = $"Failed to set stock: {ex.Message}";
        }
    }
}

public class InventoryItemViewModel : ViewModelBase
{
    private readonly InventoryItemDto _dto;

    public Guid ProductId => _dto.ProductId;
    public string ProductName => _dto.ProductName;
    public Guid CategoryId => _dto.CategoryId;
    public string CategoryName => _dto.CategoryName;
    public decimal UnitPrice => _dto.UnitPrice;
    public int CurrentStock => _dto.CurrentStock;
    public int LowStockThreshold => _dto.LowStockThreshold;
    public bool IsAvailable => _dto.IsAvailable;
    public bool IsActive => _dto.IsActive;
    public string StockStatus => _dto.StockStatus;
    public DateTime? LastRestockedAt => _dto.LastRestockedAt;

    public string FormattedPrice => $"₹{UnitPrice:N2}";
    public string FormattedLastRestocked => LastRestockedAt.HasValue
        ? LastRestockedAt.Value.ToLocalTime().ToString("dd MMM yyyy, HH:mm")
        : "Never";

    public bool IsOutOfStock => StockStatus == "Out of Stock";
    public bool IsLowStock => StockStatus == "Low Stock";
    public bool IsInStock => StockStatus == "In Stock";

    public string StatusBadgeColor => StockStatus switch
    {
        "Out of Stock" => "#DC2626", // Red
        "Low Stock" => "#D97706",    // Amber
        _ => "#16A34A"               // Green
    };

    public string StatusBadgeBackground => StockStatus switch
    {
        "Out of Stock" => "#FEE2E2",
        "Low Stock" => "#FEF3C7",
        _ => "#DCFCE7"
    };

    public InventoryItemViewModel(InventoryItemDto dto)
    {
        _dto = dto;
    }
}

public class CategoryFilterItemViewModel : ViewModelBase
{
    private bool _isSelected;

    public Guid? CategoryId { get; }
    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public CategoryFilterItemViewModel(Guid? categoryId, string name, bool isSelected = false)
    {
        CategoryId = categoryId;
        Name = name;
        _isSelected = isSelected;
    }
}
