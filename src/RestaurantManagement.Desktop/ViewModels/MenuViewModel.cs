using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Menu.DTOs;
using RestaurantManagement.Application.Menu.Interfaces;

namespace RestaurantManagement.Desktop.ViewModels;

public class MenuViewModel : ViewModelBase
{
    private readonly ICategoryService _categoryService;
    private readonly IProductService _productService;
    private readonly ILogger<MenuViewModel> _logger;

    private List<ProductDto> _allProducts = new();
    private CategoryDto? _selectedCategory;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string? _statusMessage;
    private string? _errorMessage;

    // Category Form State
    private bool _isCategoryFormOpen;
    private bool _isEditingCategory;
    private Guid? _editingCategoryId;
    private string _categoryFormName = string.Empty;
    private string? _categoryFormDescription;
    private int _categoryFormDisplayOrder;
    private string? _categoryFormError;

    // Product Form State
    private bool _isProductFormOpen;
    private bool _isEditingProduct;
    private Guid? _editingProductId;
    private string _productFormName = string.Empty;
    private decimal _productFormPrice = 0.00m;
    private Guid _productFormCategoryId;
    private string? _productFormDescription;
    private int _productFormDisplayOrder;
    private bool _productFormIsAvailable = true;
    private string? _productFormError;

    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<ProductDto> FilteredProducts { get; } = new();

    public CategoryDto? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
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
        set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    // Category Form Bindings
    public bool IsCategoryFormOpen
    {
        get => _isCategoryFormOpen;
        set => SetProperty(ref _isCategoryFormOpen, value);
    }

    public string CategoryFormTitle => _isEditingCategory ? "Edit Category" : "Add New Category";

    public string CategoryFormName
    {
        get => _categoryFormName;
        set => SetProperty(ref _categoryFormName, value);
    }

    public string? CategoryFormDescription
    {
        get => _categoryFormDescription;
        set => SetProperty(ref _categoryFormDescription, value);
    }

    public int CategoryFormDisplayOrder
    {
        get => _categoryFormDisplayOrder;
        set => SetProperty(ref _categoryFormDisplayOrder, value);
    }

    public string? CategoryFormError
    {
        get => _categoryFormError;
        set => SetProperty(ref _categoryFormError, value);
    }

    // Product Form Bindings
    public bool IsProductFormOpen
    {
        get => _isProductFormOpen;
        set => SetProperty(ref _isProductFormOpen, value);
    }

    public string ProductFormTitle => _isEditingProduct ? "Edit Product" : "Add New Product";

    public string ProductFormName
    {
        get => _productFormName;
        set => SetProperty(ref _productFormName, value);
    }

    public decimal ProductFormPrice
    {
        get => _productFormPrice;
        set => SetProperty(ref _productFormPrice, value);
    }

    public Guid ProductFormCategoryId
    {
        get => _productFormCategoryId;
        set => SetProperty(ref _productFormCategoryId, value);
    }

    public string? ProductFormDescription
    {
        get => _productFormDescription;
        set => SetProperty(ref _productFormDescription, value);
    }

    public int ProductFormDisplayOrder
    {
        get => _productFormDisplayOrder;
        set => SetProperty(ref _productFormDisplayOrder, value);
    }

    public bool ProductFormIsAvailable
    {
        get => _productFormIsAvailable;
        set => SetProperty(ref _productFormIsAvailable, value);
    }

    public string? ProductFormError
    {
        get => _productFormError;
        set => SetProperty(ref _productFormError, value);
    }

    // Commands
    public ICommand LoadDataCommand { get; }
    public ICommand ClearCategoryFilterCommand { get; }
    public ICommand OpenAddCategoryCommand { get; }
    public ICommand OpenEditCategoryCommand { get; }
    public ICommand SaveCategoryCommand { get; }
    public ICommand CancelCategoryFormCommand { get; }
    public ICommand ToggleCategoryActiveCommand { get; }
    public ICommand DeleteCategoryCommand { get; }

    public ICommand OpenAddProductCommand { get; }
    public ICommand OpenEditProductCommand { get; }
    public ICommand SaveProductCommand { get; }
    public ICommand CancelProductFormCommand { get; }
    public ICommand ToggleProductAvailabilityCommand { get; }
    public ICommand ToggleProductActiveCommand { get; }
    public ICommand DeleteProductCommand { get; }

    public MenuViewModel(
        ICategoryService categoryService,
        IProductService productService,
        ILogger<MenuViewModel> logger)
    {
        _categoryService = categoryService;
        _productService = productService;
        _logger = logger;

        LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());
        ClearCategoryFilterCommand = new RelayCommand(() => SelectedCategory = null);

        OpenAddCategoryCommand = new RelayCommand(OpenAddCategory);
        OpenEditCategoryCommand = new RelayCommand<CategoryDto>(OpenEditCategory);
        SaveCategoryCommand = new RelayCommand(async () => await SaveCategoryAsync());
        CancelCategoryFormCommand = new RelayCommand(() => IsCategoryFormOpen = false);
        ToggleCategoryActiveCommand = new RelayCommand<CategoryDto>(async c => await ToggleCategoryActiveAsync(c));
        DeleteCategoryCommand = new RelayCommand<CategoryDto>(async c => await DeleteCategoryAsync(c));

        OpenAddProductCommand = new RelayCommand(OpenAddProduct);
        OpenEditProductCommand = new RelayCommand<ProductDto>(OpenEditProduct);
        SaveProductCommand = new RelayCommand(async () => await SaveProductAsync());
        CancelProductFormCommand = new RelayCommand(() => IsProductFormOpen = false);
        ToggleProductAvailabilityCommand = new RelayCommand<ProductDto>(async p => await ToggleProductAvailabilityAsync(p));
        ToggleProductActiveCommand = new RelayCommand<ProductDto>(async p => await ToggleProductActiveAsync(p));
        DeleteProductCommand = new RelayCommand<ProductDto>(async p => await DeleteProductAsync(p));

        _ = LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var categories = await _categoryService.GetCategoriesAsync(includeInactive: true);
            var products = await _productService.GetProductsAsync(includeInactive: true);

            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            _allProducts = products.ToList();
            ApplyProductFilter();

            StatusMessage = $"Loaded {Categories.Count} categories and {_allProducts.Count} products.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading menu data.");
            ErrorMessage = "Failed to load menu data. Please check logs.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyProductFilter()
    {
        FilteredProducts.Clear();
        var query = _allProducts.AsEnumerable();

        if (SelectedCategory != null)
        {
            query = query.Where(p => p.CategoryId == SelectedCategory.Id);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLowerInvariant().Contains(term) ||
                                     (p.Description != null && p.Description.ToLowerInvariant().Contains(term)));
        }

        foreach (var product in query)
        {
            FilteredProducts.Add(product);
        }
    }

    private void OpenAddCategory()
    {
        _isEditingCategory = false;
        _editingCategoryId = null;
        CategoryFormName = string.Empty;
        CategoryFormDescription = string.Empty;
        CategoryFormDisplayOrder = Categories.Count + 1;
        CategoryFormError = null;
        OnPropertyChanged(nameof(CategoryFormTitle));
        IsCategoryFormOpen = true;
    }

    private void OpenEditCategory(CategoryDto? category)
    {
        if (category == null) return;

        _isEditingCategory = true;
        _editingCategoryId = category.Id;
        CategoryFormName = category.Name;
        CategoryFormDescription = category.Description;
        CategoryFormDisplayOrder = category.DisplayOrder;
        CategoryFormError = null;
        OnPropertyChanged(nameof(CategoryFormTitle));
        IsCategoryFormOpen = true;
    }

    private async Task SaveCategoryAsync()
    {
        CategoryFormError = null;
        try
        {
            if (string.IsNullOrWhiteSpace(CategoryFormName))
            {
                CategoryFormError = "Category name is required.";
                return;
            }

            if (_isEditingCategory && _editingCategoryId.HasValue)
            {
                var existing = Categories.FirstOrDefault(c => c.Id == _editingCategoryId.Value);
                var request = new UpdateCategoryRequest(
                    _editingCategoryId.Value,
                    CategoryFormName,
                    CategoryFormDescription,
                    CategoryFormDisplayOrder,
                    existing?.IsActive ?? true);

                await _categoryService.UpdateCategoryAsync(request);
            }
            else
            {
                var request = new CreateCategoryRequest(
                    CategoryFormName,
                    CategoryFormDescription,
                    CategoryFormDisplayOrder,
                    true);

                await _categoryService.CreateCategoryAsync(request);
            }

            IsCategoryFormOpen = false;
            await LoadDataAsync();
        }
        catch (ValidationException vex)
        {
            CategoryFormError = vex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving category.");
            CategoryFormError = "An unexpected error occurred while saving.";
        }
    }

    private async Task ToggleCategoryActiveAsync(CategoryDto? category)
    {
        if (category == null) return;
        try
        {
            if (category.IsActive)
            {
                await _categoryService.DeactivateCategoryAsync(category.Id);
            }
            else
            {
                await _categoryService.ActivateCategoryAsync(category.Id);
            }
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling category status.");
            ErrorMessage = "Failed to update category status.";
        }
    }

    private void OpenAddProduct()
    {
        if (Categories.Count == 0)
        {
            ErrorMessage = "Please create at least one category before adding products.";
            return;
        }

        _isEditingProduct = false;
        _editingProductId = null;
        ProductFormName = string.Empty;
        ProductFormPrice = 9.99m;
        ProductFormCategoryId = SelectedCategory?.Id ?? Categories.First().Id;
        ProductFormDescription = string.Empty;
        ProductFormDisplayOrder = FilteredProducts.Count + 1;
        ProductFormIsAvailable = true;
        ProductFormError = null;
        OnPropertyChanged(nameof(ProductFormTitle));
        IsProductFormOpen = true;
    }

    private void OpenEditProduct(ProductDto? product)
    {
        if (product == null) return;

        _isEditingProduct = true;
        _editingProductId = product.Id;
        ProductFormName = product.Name;
        ProductFormPrice = product.Price;
        ProductFormCategoryId = product.CategoryId;
        ProductFormDescription = product.Description;
        ProductFormDisplayOrder = product.DisplayOrder;
        ProductFormIsAvailable = product.IsAvailable;
        ProductFormError = null;
        OnPropertyChanged(nameof(ProductFormTitle));
        IsProductFormOpen = true;
    }

    private async Task SaveProductAsync()
    {
        ProductFormError = null;
        try
        {
            if (string.IsNullOrWhiteSpace(ProductFormName))
            {
                ProductFormError = "Product name is required.";
                return;
            }

            if (ProductFormPrice < 0)
            {
                ProductFormError = "Price cannot be negative.";
                return;
            }

            if (ProductFormCategoryId == Guid.Empty)
            {
                ProductFormError = "Please select a valid category.";
                return;
            }

            if (_isEditingProduct && _editingProductId.HasValue)
            {
                var existing = _allProducts.FirstOrDefault(p => p.Id == _editingProductId.Value);
                var request = new UpdateProductRequest(
                    _editingProductId.Value,
                    ProductFormName,
                    ProductFormPrice,
                    ProductFormCategoryId,
                    ProductFormDescription,
                    ProductFormDisplayOrder,
                    existing?.IsActive ?? true,
                    ProductFormIsAvailable);

                await _productService.UpdateProductAsync(request);
            }
            else
            {
                var request = new CreateProductRequest(
                    ProductFormName,
                    ProductFormPrice,
                    ProductFormCategoryId,
                    ProductFormDescription,
                    ProductFormDisplayOrder,
                    true,
                    ProductFormIsAvailable);

                await _productService.CreateProductAsync(request);
            }

            IsProductFormOpen = false;
            await LoadDataAsync();
        }
        catch (ValidationException vex)
        {
            ProductFormError = vex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving product.");
            ProductFormError = "An unexpected error occurred while saving.";
        }
    }

    private async Task ToggleProductAvailabilityAsync(ProductDto? product)
    {
        if (product == null) return;
        try
        {
            await _productService.SetProductAvailabilityAsync(product.Id, !product.IsAvailable);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling product availability.");
            ErrorMessage = "Failed to update product availability.";
        }
    }

    private async Task ToggleProductActiveAsync(ProductDto? product)
    {
        if (product == null) return;
        try
        {
            if (product.IsActive)
            {
                await _productService.DeactivateProductAsync(product.Id);
            }
            else
            {
                await _productService.ActivateProductAsync(product.Id);
            }
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling product status.");
            ErrorMessage = "Failed to update product active status.";
        }
    }

    private async Task DeleteCategoryAsync(CategoryDto? category)
    {
        var targetCat = category ?? (_editingCategoryId.HasValue ? Categories.FirstOrDefault(c => c.Id == _editingCategoryId.Value) : null);
        if (targetCat == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete Category '{targetCat.Name}'?",
            "Confirm Delete Category",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            ErrorMessage = null;
            await _categoryService.DeleteCategoryAsync(targetCat.Id);
            IsCategoryFormOpen = false;
            await LoadDataAsync();
            StatusMessage = $"Category '{targetCat.Name}' deleted.";
        }
        catch (ValidationException vex)
        {
            ErrorMessage = vex.Message;
            if (IsCategoryFormOpen)
            {
                CategoryFormError = vex.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category '{CategoryName}'.", targetCat.Name);
            ErrorMessage = "Failed to delete category. Please check system logs.";
        }
    }

    private async Task DeleteProductAsync(ProductDto? product)
    {
        var targetProd = product ?? (_editingProductId.HasValue ? _allProducts.FirstOrDefault(p => p.Id == _editingProductId.Value) : null);
        if (targetProd == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to permanently delete menu item '{targetProd.Name}'?",
            "Confirm Delete Menu Item",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            ErrorMessage = null;
            await _productService.DeleteProductAsync(targetProd.Id);
            IsProductFormOpen = false;
            await LoadDataAsync();
            StatusMessage = $"Menu item '{targetProd.Name}' deleted.";
        }
        catch (ValidationException vex)
        {
            ErrorMessage = vex.Message;
            if (IsProductFormOpen)
            {
                ProductFormError = vex.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product '{ProductName}'.", targetProd.Name);
            ErrorMessage = "Failed to delete product. Please check system logs.";
        }
    }
}
