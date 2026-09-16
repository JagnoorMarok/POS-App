namespace RestaurantManagement.Desktop.ViewModels;

/// <summary>
/// ViewModel representing a navigation entry in the application shell.
/// </summary>
public class NavigationItemViewModel : ViewModelBase
{
    private bool _isSelected;

    public string Title { get; }
    public string Tag { get; }
    public string IconGlyph { get; }
    public string Description { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public NavigationItemViewModel(string title, string tag, string iconGlyph, string description, bool isSelected = false)
    {
        Title = title;
        Tag = tag;
        IconGlyph = iconGlyph;
        Description = description;
        IsSelected = isSelected;
    }
}
