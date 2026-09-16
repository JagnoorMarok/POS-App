using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Tables.DTOs;
using RestaurantManagement.Application.Tables.Interfaces;

namespace RestaurantManagement.Desktop.ViewModels;

public class TablesViewModel : ViewModelBase
{
    private readonly ITableService _tableService;
    private readonly ILogger<TablesViewModel> _logger;

    private List<RestaurantTableDto> _allTables = new();
    private bool _isLoading;
    private string? _statusMessage;
    private string? _errorMessage;
    private string _filterMode = "All"; // "All", "Free", "Occupied"

    // Table Form State
    private bool _isTableFormOpen;
    private bool _isEditingTable;
    private Guid? _editingTableId;
    private string _tableFormNumber = string.Empty;
    private int _tableFormCapacity = 4;
    private int _tableFormDisplayOrder;
    private string? _tableFormError;

    public ObservableCollection<RestaurantTableDto> FilteredTables { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string FilterMode
    {
        get => _filterMode;
        set
        {
            if (SetProperty(ref _filterMode, value))
            {
                ApplyTableFilter();
            }
        }
    }

    public int TotalTablesCount => _allTables.Count;
    public int FreeTablesCount => _allTables.Count(t => !t.IsOccupied && t.IsActive);
    public int OccupiedTablesCount => _allTables.Count(t => t.IsOccupied && t.IsActive);

    // Form Bindings
    public bool IsTableFormOpen
    {
        get => _isTableFormOpen;
        set => SetProperty(ref _isTableFormOpen, value);
    }

    public string TableFormTitle => _isEditingTable ? "Edit Table" : "Add New Table";

    public string TableFormNumber
    {
        get => _tableFormNumber;
        set => SetProperty(ref _tableFormNumber, value);
    }

    public int TableFormCapacity
    {
        get => _tableFormCapacity;
        set => SetProperty(ref _tableFormCapacity, value);
    }

    public int TableFormDisplayOrder
    {
        get => _tableFormDisplayOrder;
        set => SetProperty(ref _tableFormDisplayOrder, value);
    }

    public string? TableFormError
    {
        get => _tableFormError;
        set => SetProperty(ref _tableFormError, value);
    }

    // Commands
    public ICommand LoadTablesCommand { get; }
    public ICommand SetFilterModeCommand { get; }
    public ICommand OpenAddTableCommand { get; }
    public ICommand OpenEditTableCommand { get; }
    public ICommand SaveTableCommand { get; }
    public ICommand CancelTableFormCommand { get; }
    public ICommand ToggleOccupancyCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public ICommand DeleteTableCommand { get; }

    public TablesViewModel(
        ITableService tableService,
        ILogger<TablesViewModel> logger)
    {
        _tableService = tableService;
        _logger = logger;

        LoadTablesCommand = new RelayCommand(async () => await LoadTablesAsync());
        SetFilterModeCommand = new RelayCommand<string>(mode => FilterMode = mode ?? "All");

        OpenAddTableCommand = new RelayCommand(OpenAddTable);
        OpenEditTableCommand = new RelayCommand<RestaurantTableDto>(OpenEditTable);
        SaveTableCommand = new RelayCommand(async () => await SaveTableAsync());
        CancelTableFormCommand = new RelayCommand(() => IsTableFormOpen = false);
        ToggleOccupancyCommand = new RelayCommand<RestaurantTableDto>(async t => await ToggleOccupancyAsync(t));
        ToggleActiveCommand = new RelayCommand<RestaurantTableDto>(async t => await ToggleActiveAsync(t));
        DeleteTableCommand = new RelayCommand<RestaurantTableDto>(async t => await DeleteTableAsync(t));

        _ = LoadTablesAsync();
    }

    public async Task LoadTablesAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var tables = await _tableService.GetAllTablesAsync(includeInactive: true);
            _allTables = tables.ToList();

            ApplyTableFilter();

            OnPropertyChanged(nameof(TotalTablesCount));
            OnPropertyChanged(nameof(FreeTablesCount));
            OnPropertyChanged(nameof(OccupiedTablesCount));

            StatusMessage = $"Total: {TotalTablesCount} tables • Free: {FreeTablesCount} • Occupied: {OccupiedTablesCount}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tables.");
            ErrorMessage = "Failed to load tables. Please check system logs.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyTableFilter()
    {
        FilteredTables.Clear();
        var query = _allTables.AsEnumerable();

        if (FilterMode == "Free")
        {
            query = query.Where(t => !t.IsOccupied && t.IsActive);
        }
        else if (FilterMode == "Occupied")
        {
            query = query.Where(t => t.IsOccupied && t.IsActive);
        }

        foreach (var table in query)
        {
            FilteredTables.Add(table);
        }
    }

    private void OpenAddTable()
    {
        _isEditingTable = false;
        _editingTableId = null;
        TableFormNumber = $"T{_allTables.Count + 1}";
        TableFormCapacity = 4;
        TableFormDisplayOrder = _allTables.Count + 1;
        TableFormError = null;
        OnPropertyChanged(nameof(TableFormTitle));
        IsTableFormOpen = true;
    }

    private void OpenEditTable(RestaurantTableDto? table)
    {
        if (table == null) return;

        _isEditingTable = true;
        _editingTableId = table.Id;
        TableFormNumber = table.TableNumber;
        TableFormCapacity = table.Capacity;
        TableFormDisplayOrder = table.DisplayOrder;
        TableFormError = null;
        OnPropertyChanged(nameof(TableFormTitle));
        IsTableFormOpen = true;
    }

    private async Task SaveTableAsync()
    {
        TableFormError = null;
        try
        {
            if (string.IsNullOrWhiteSpace(TableFormNumber))
            {
                TableFormError = "Table number is required.";
                return;
            }

            if (TableFormCapacity <= 0)
            {
                TableFormError = "Capacity must be greater than zero.";
                return;
            }

            if (_isEditingTable && _editingTableId.HasValue)
            {
                var existing = _allTables.FirstOrDefault(t => t.Id == _editingTableId.Value);
                var request = new UpdateTableRequest(
                    _editingTableId.Value,
                    TableFormNumber,
                    TableFormCapacity,
                    TableFormDisplayOrder,
                    existing?.IsActive ?? true);

                await _tableService.UpdateTableAsync(request);
            }
            else
            {
                var request = new CreateTableRequest(
                    TableFormNumber,
                    TableFormCapacity,
                    TableFormDisplayOrder,
                    true);

                await _tableService.CreateTableAsync(request);
            }

            IsTableFormOpen = false;
            await LoadTablesAsync();
        }
        catch (ValidationException vex)
        {
            TableFormError = vex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving table.");
            TableFormError = "An unexpected error occurred while saving the table.";
        }
    }

    private async Task ToggleOccupancyAsync(RestaurantTableDto? table)
    {
        if (table == null) return;
        try
        {
            await _tableService.SetTableOccupancyAsync(table.Id, !table.IsOccupied);
            await LoadTablesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing table occupancy.");
            ErrorMessage = "Failed to update table occupancy.";
        }
    }

    private async Task ToggleActiveAsync(RestaurantTableDto? table)
    {
        if (table == null) return;
        try
        {
            if (table.IsActive)
            {
                await _tableService.DeactivateTableAsync(table.Id);
            }
            else
            {
                await _tableService.ActivateTableAsync(table.Id);
            }
            await LoadTablesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling table active status.");
            ErrorMessage = "Failed to update table active status.";
        }
    }

    private async Task DeleteTableAsync(RestaurantTableDto? table)
    {
        var targetTable = table ?? (_editingTableId.HasValue ? _allTables.FirstOrDefault(t => t.Id == _editingTableId.Value) : null);
        if (targetTable == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to permanently delete Table {targetTable.TableNumber}?",
            "Confirm Delete Table",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            ErrorMessage = null;
            await _tableService.DeleteTableAsync(targetTable.Id);
            IsTableFormOpen = false;
            await LoadTablesAsync();
            StatusMessage = $"Table {targetTable.TableNumber} was deleted successfully.";
        }
        catch (ValidationException vex)
        {
            ErrorMessage = vex.Message;
            if (IsTableFormOpen)
            {
                TableFormError = vex.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting table {TableNumber}.", targetTable.TableNumber);
            ErrorMessage = "Failed to delete table. Please check system logs.";
        }
    }
}
