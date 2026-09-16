using Microsoft.Extensions.Logging;
using RestaurantManagement.Desktop.ViewModels;

namespace RestaurantManagement.Desktop.Services;

public class NavigationService : INavigationService
{
    private readonly ILogger<NavigationService> _logger;

    public event Action<NavigationItemViewModel>? CurrentViewChanged;

    public NavigationService(ILogger<NavigationService> logger)
    {
        _logger = logger;
    }

    public void NavigateTo(NavigationItemViewModel item)
    {
        _logger.LogInformation("Navigating to view section: {SectionTitle} ({SectionTag})", item.Title, item.Tag);
        CurrentViewChanged?.Invoke(item);
    }
}
