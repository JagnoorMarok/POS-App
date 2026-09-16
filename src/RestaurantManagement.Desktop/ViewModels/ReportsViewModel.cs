using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Reports.DTOs;
using RestaurantManagement.Application.Reports.Interfaces;

namespace RestaurantManagement.Desktop.ViewModels;

public class ReportsViewModel : ViewModelBase
{
    private readonly IReportService _reportService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ReportsViewModel> _logger;

    private bool _isLoading;
    private string? _errorMessage;
    private string _selectedPeriod = "Today";
    private string _periodLabel = "Today";
    private DateTime _fromDate;
    private DateTime _toDate;

    private decimal _grossSales;
    private decimal _totalDiscounts;
    private decimal _totalTaxes;
    private decimal _netRevenue;
    private decimal _totalPaidAmount;
    private decimal _totalOutstanding;
    private int _totalOrders;
    private int _completedOrdersCount;
    private int _cancelledOrdersCount;
    private decimal _averageOrderValue;

    public ObservableCollection<PaymentMethodSalesDto> PaymentMethodBreakdown { get; } = new();
    public ObservableCollection<OrderTypeSalesDto> OrderTypeBreakdown { get; } = new();
    public ObservableCollection<TopSellingProductDto> TopSellingProducts { get; } = new();
    public ObservableCollection<DailySalesPointDto> DailyTrend { get; } = new();

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

    public string SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (SetProperty(ref _selectedPeriod, value))
            {
                _ = GenerateReportAsync();
            }
        }
    }

    public string PeriodLabel
    {
        get => _periodLabel;
        set => SetProperty(ref _periodLabel, value);
    }

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public decimal GrossSales
    {
        get => _grossSales;
        set => SetProperty(ref _grossSales, value);
    }

    public decimal TotalDiscounts
    {
        get => _totalDiscounts;
        set => SetProperty(ref _totalDiscounts, value);
    }

    public decimal TotalTaxes
    {
        get => _totalTaxes;
        set => SetProperty(ref _totalTaxes, value);
    }

    public decimal NetRevenue
    {
        get => _netRevenue;
        set => SetProperty(ref _netRevenue, value);
    }

    public decimal TotalPaidAmount
    {
        get => _totalPaidAmount;
        set => SetProperty(ref _totalPaidAmount, value);
    }

    public decimal TotalOutstanding
    {
        get => _totalOutstanding;
        set => SetProperty(ref _totalOutstanding, value);
    }

    public int TotalOrders
    {
        get => _totalOrders;
        set => SetProperty(ref _totalOrders, value);
    }

    public int CompletedOrdersCount
    {
        get => _completedOrdersCount;
        set => SetProperty(ref _completedOrdersCount, value);
    }

    public int CancelledOrdersCount
    {
        get => _cancelledOrdersCount;
        set => SetProperty(ref _cancelledOrdersCount, value);
    }

    public decimal AverageOrderValue
    {
        get => _averageOrderValue;
        set => SetProperty(ref _averageOrderValue, value);
    }

    public ICommand SelectPeriodCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ApplyCustomDatesCommand { get; }

    public ReportsViewModel(
        IReportService reportService,
        IDateTimeProvider dateTimeProvider,
        ILogger<ReportsViewModel> logger)
    {
        _reportService = reportService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;

        _fromDate = _dateTimeProvider.Now.Date;
        _toDate = _dateTimeProvider.Now.Date;

        SelectPeriodCommand = new RelayCommand<string>(period =>
        {
            if (!string.IsNullOrEmpty(period))
            {
                SelectedPeriod = period;
            }
        });

        RefreshCommand = new RelayCommand(async _ => await GenerateReportAsync());
        ApplyCustomDatesCommand = new RelayCommand(async _ =>
        {
            SelectedPeriod = "Custom";
            await GenerateReportAsync();
        });
    }

    public async Task GenerateReportAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            GenerateReportRequest req;
            if (SelectedPeriod == "Custom")
            {
                var fromUtc = DateTime.SpecifyKind(FromDate.Date, DateTimeKind.Utc);
                var toUtc = DateTime.SpecifyKind(ToDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                req = new GenerateReportRequest(fromUtc, toUtc, "Custom");
            }
            else
            {
                req = new GenerateReportRequest(PresetPeriod: SelectedPeriod);
            }

            var report = await _reportService.GenerateSalesReportAsync(req);

            PeriodLabel = report.PeriodLabel;
            GrossSales = report.GrossSales;
            TotalDiscounts = report.TotalDiscounts;
            TotalTaxes = report.TotalTaxes;
            NetRevenue = report.NetRevenue;
            TotalPaidAmount = report.TotalPaidAmount;
            TotalOutstanding = report.TotalOutstanding;
            TotalOrders = report.TotalOrders;
            CompletedOrdersCount = report.CompletedOrdersCount;
            CancelledOrdersCount = report.CancelledOrdersCount;
            AverageOrderValue = report.AverageOrderValue;

            PaymentMethodBreakdown.Clear();
            foreach (var p in report.PaymentMethods)
            {
                PaymentMethodBreakdown.Add(p);
            }

            OrderTypeBreakdown.Clear();
            foreach (var ot in report.OrderTypes)
            {
                OrderTypeBreakdown.Add(ot);
            }

            TopSellingProducts.Clear();
            foreach (var prod in report.TopProducts)
            {
                TopSellingProducts.Add(prod);
            }

            DailyTrend.Clear();
            foreach (var d in report.DailyTrend)
            {
                DailyTrend.Add(d);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate sales report.");
            ErrorMessage = "Failed to compile sales report. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
