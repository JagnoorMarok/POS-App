using RestaurantManagement.Desktop.ViewModels;

namespace RestaurantManagement.Desktop.Services;

/// <summary>
/// Navigation service abstraction for shell view transitions.
/// </summary>
public interface INavigationService
{
    event Action<NavigationItemViewModel>? CurrentViewChanged;
    void NavigateTo(NavigationItemViewModel item);
}
