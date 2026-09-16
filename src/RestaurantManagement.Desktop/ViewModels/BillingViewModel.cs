using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Billing.DTOs;
using RestaurantManagement.Application.Billing.Interfaces;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Desktop.ViewModels;

public class BillingOrderSummaryItemViewModel : ViewModelBase
{
    private readonly BillSummaryDto _dto;
    private bool _isSelected;

    public Guid OrderId => _dto.OrderId;
    public string OrderNumber => _dto.OrderNumber;
    public OrderType OrderType => _dto.OrderType;
    public OrderStatus OrderStatus => _dto.OrderStatus;
    public Guid? RestaurantTableId => _dto.RestaurantTableId;
    public string? TableNumber => _dto.TableNumber;
    public DateTime CreatedAt => _dto.CreatedAt;
    public decimal TotalAmount => _dto.TotalAmount;
    public decimal PaidAmount => _dto.PaidAmount;
    public decimal RemainingAmount => _dto.RemainingAmount;
    public bool IsFullyPaid => _dto.IsFullyPaid;
    public int ItemCount => _dto.ItemCount;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string TableOrTypeBadge => OrderType switch
    {
        OrderType.DineIn => $"🪑 {TableNumber ?? "Table"}",
        OrderType.Takeaway => "🛍️ Takeaway",
        OrderType.Delivery => "🛵 Delivery",
        _ => OrderType.ToString()
    };

    public string StatusBadge => OrderStatus switch
    {
        OrderStatus.Confirmed => "Kitchen Queued",
        OrderStatus.Preparing => "In Kitchen",
        OrderStatus.Ready => "Ready",
        OrderStatus.Served => "Served",
        _ => OrderStatus.ToString()
    };

    public BillingOrderSummaryItemViewModel(BillSummaryDto dto, bool isSelected = false)
    {
        _dto = dto;
        _isSelected = isSelected;
    }
}

public class BillingViewModel : ViewModelBase
{
    private readonly IBillingService _billingService;
    private readonly IPaymentService _paymentService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<BillingViewModel> _logger;

    public ObservableCollection<BillingOrderSummaryItemViewModel> OrdersAwaitingBilling { get; } = new();
    public ObservableCollection<PaymentMethod> AvailablePaymentMethods { get; } = new();

    private BillingOrderSummaryItemViewModel? _selectedOrderSummary;
    private BillDto? _currentBill;
    private bool _isLoading;
    private string? _statusMessage;
    private string? _errorMessage;

    // Payment Form fields
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
    private decimal _paymentAmountInput;
    private decimal? _cashTenderedInput;
    private decimal _calculatedChange;
    private string? _transactionReferenceInput;

    // Discount Form field
    private decimal _discountInput;

    public BillingOrderSummaryItemViewModel? SelectedOrderSummary
    {
        get => _selectedOrderSummary;
        set
        {
            if (SetProperty(ref _selectedOrderSummary, value))
            {
                foreach (var item in OrdersAwaitingBilling)
                {
                    item.IsSelected = (item == value);
                }

                if (value != null)
                {
                    _ = LoadBillForOrderAsync(value.OrderId);
                }
                else
                {
                    CurrentBill = null;
                }
            }
        }
    }

    public BillDto? CurrentBill
    {
        get => _currentBill;
        private set
        {
            if (SetProperty(ref _currentBill, value))
            {
                OnPropertyChanged(nameof(HasSelectedBill));
                OnPropertyChanged(nameof(HasNoSelectedBill));
                OnPropertyChanged(nameof(CanApplyDiscount));
                OnPropertyChanged(nameof(CanRecordPayment));
                OnPropertyChanged(nameof(CanCompleteOrder));

                if (value != null)
                {
                    PaymentAmountInput = value.RemainingAmount;
                    DiscountInput = value.DiscountAmount;
                    ResetPaymentTenderFields();
                }
            }
        }
    }

    public bool HasSelectedBill => CurrentBill != null;
    public bool HasNoSelectedBill => CurrentBill == null;

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

    public PaymentMethod SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set
        {
            if (SetProperty(ref _selectedPaymentMethod, value))
            {
                OnPropertyChanged(nameof(IsCashPayment));
                RecalculateChange();
            }
        }
    }

    public bool IsCashPayment => SelectedPaymentMethod == PaymentMethod.Cash;

    public decimal PaymentAmountInput
    {
        get => _paymentAmountInput;
        set
        {
            if (SetProperty(ref _paymentAmountInput, value))
            {
                RecalculateChange();
                OnPropertyChanged(nameof(CanRecordPayment));
            }
        }
    }

    public decimal? CashTenderedInput
    {
        get => _cashTenderedInput;
        set
        {
            if (SetProperty(ref _cashTenderedInput, value))
            {
                RecalculateChange();
            }
        }
    }

    public decimal CalculatedChange
    {
        get => _calculatedChange;
        private set => SetProperty(ref _calculatedChange, value);
    }

    public string? TransactionReferenceInput
    {
        get => _transactionReferenceInput;
        set => SetProperty(ref _transactionReferenceInput, value);
    }

    public decimal DiscountInput
    {
        get => _discountInput;
        set => SetProperty(ref _discountInput, value);
    }

    public bool CanApplyDiscount => CurrentBill != null && CurrentBill.OrderStatus != OrderStatus.Completed && CurrentBill.OrderStatus != OrderStatus.Cancelled;
    public bool CanRecordPayment => CurrentBill != null && CurrentBill.RemainingAmount > 0 && PaymentAmountInput > 0 && PaymentAmountInput <= CurrentBill.RemainingAmount;
    public bool CanCompleteOrder => CurrentBill != null && CurrentBill.RemainingAmount == 0 && CurrentBill.TotalAmount > 0 && CurrentBill.OrderStatus != OrderStatus.Completed;

    public ICommand RefreshOrdersCommand { get; }
    public ICommand SelectOrderCommand { get; }
    public ICommand RecordPaymentCommand { get; }
    public ICommand ApplyDiscountCommand { get; }
    public ICommand SettleOrderCommand { get; }
    public ICommand SetQuickTenderCommand { get; }
    public ICommand FillExactRemainingCommand { get; }

    public BillingViewModel(
        IBillingService billingService,
        IPaymentService paymentService,
        IDateTimeProvider dateTimeProvider,
        ILogger<BillingViewModel> logger)
    {
        _billingService = billingService;
        _paymentService = paymentService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;

        foreach (PaymentMethod method in Enum.GetValues<PaymentMethod>())
        {
            AvailablePaymentMethods.Add(method);
        }

        RefreshOrdersCommand = new RelayCommand(async _ => await LoadBillingOrdersAsync());
        SelectOrderCommand = new RelayCommand<BillingOrderSummaryItemViewModel>(item =>
        {
            if (item != null)
            {
                SelectedOrderSummary = item;
            }
        });

        RecordPaymentCommand = new RelayCommand(async _ => await ExecuteRecordPaymentAsync(), _ => CanRecordPayment);
        ApplyDiscountCommand = new RelayCommand(async _ => await ExecuteApplyDiscountAsync(), _ => CanApplyDiscount);
        SettleOrderCommand = new RelayCommand(async _ => await ExecuteSettleOrderAsync(), _ => CanCompleteOrder);

        SetQuickTenderCommand = new RelayCommand<object>(param =>
        {
            if (param != null && decimal.TryParse(param.ToString(), out var amount))
            {
                CashTenderedInput = amount;
            }
        });

        FillExactRemainingCommand = new RelayCommand(_ =>
        {
            if (CurrentBill != null)
            {
                PaymentAmountInput = CurrentBill.RemainingAmount;
                if (IsCashPayment)
                {
                    CashTenderedInput = CurrentBill.RemainingAmount;
                }
            }
        });

        _ = LoadBillingOrdersAsync();
    }

    public async Task LoadBillingOrdersAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var summaries = await _billingService.GetOrdersAwaitingBillingAsync();
            OrdersAwaitingBilling.Clear();

            BillingOrderSummaryItemViewModel? toReselect = null;
            foreach (var summary in summaries)
            {
                var isSelected = _selectedOrderSummary != null && _selectedOrderSummary.OrderId == summary.OrderId;
                var item = new BillingOrderSummaryItemViewModel(summary, isSelected);
                OrdersAwaitingBilling.Add(item);

                if (isSelected)
                {
                    toReselect = item;
                }
            }

            if (toReselect != null)
            {
                SelectedOrderSummary = toReselect;
            }
            else if (OrdersAwaitingBilling.Count > 0 && SelectedOrderSummary == null)
            {
                SelectedOrderSummary = OrdersAwaitingBilling[0];
            }
            else if (OrdersAwaitingBilling.Count == 0)
            {
                SelectedOrderSummary = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load billing orders.");
            ErrorMessage = $"Failed to load orders: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadBillForOrderAsync(Guid orderId)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            CurrentBill = await _billingService.GetBillForOrderAsync(orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load bill for order {OrderId}", orderId);
            ErrorMessage = $"Failed to load bill: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RecalculateChange()
    {
        if (IsCashPayment && CashTenderedInput.HasValue && CashTenderedInput.Value >= PaymentAmountInput)
        {
            CalculatedChange = CashTenderedInput.Value - PaymentAmountInput;
        }
        else
        {
            CalculatedChange = 0m;
        }
    }

    private void ResetPaymentTenderFields()
    {
        if (CurrentBill != null)
        {
            PaymentAmountInput = CurrentBill.RemainingAmount;
            CashTenderedInput = IsCashPayment ? CurrentBill.RemainingAmount : null;
        }
        else
        {
            PaymentAmountInput = 0m;
            CashTenderedInput = null;
        }
        TransactionReferenceInput = null;
        RecalculateChange();
    }

    private async Task ExecuteRecordPaymentAsync()
    {
        if (CurrentBill == null) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusMessage = null;

            var request = new RecordPaymentRequest(
                CurrentBill.OrderId,
                PaymentAmountInput,
                SelectedPaymentMethod,
                TransactionReferenceInput,
                IsCashPayment ? CashTenderedInput : null);

            var paymentResult = await _paymentService.RecordPaymentAsync(request);

            if (paymentResult.ChangeReturned.HasValue && paymentResult.ChangeReturned.Value > 0)
            {
                StatusMessage = $"Payment of ₹{paymentResult.Amount:N2} recorded successfully! Return change: ₹{paymentResult.ChangeReturned.Value:N2}";
            }
            else
            {
                StatusMessage = $"Payment of ₹{paymentResult.Amount:N2} ({paymentResult.PaymentMethod}) recorded successfully!";
            }

            // Reload order and list
            var currentOrderId = CurrentBill.OrderId;
            await LoadBillingOrdersAsync();
            await LoadBillForOrderAsync(currentOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record payment.");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteApplyDiscountAsync()
    {
        if (CurrentBill == null) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusMessage = null;

            var request = new ApplyDiscountRequest(CurrentBill.OrderId, DiscountInput);
            CurrentBill = await _billingService.ApplyDiscountAsync(request);
            StatusMessage = $"Discount of ₹{DiscountInput:N2} applied successfully.";

            await LoadBillingOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply discount.");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteSettleOrderAsync()
    {
        if (CurrentBill == null) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusMessage = null;

            await _paymentService.CompleteOrderSettlementAsync(CurrentBill.OrderId);
            StatusMessage = $"Order {CurrentBill.OrderNumber} settled and marked COMPLETED!";

            await LoadBillingOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to settle order.");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
