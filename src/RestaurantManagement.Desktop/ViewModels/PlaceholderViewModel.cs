namespace RestaurantManagement.Desktop.ViewModels;

public class PlaceholderViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _icon = string.Empty;
    private string _description = string.Empty;
    private string _milestoneTarget = "Future Milestone";

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string MilestoneTarget
    {
        get => _milestoneTarget;
        set => SetProperty(ref _milestoneTarget, value);
    }

    public void SetContext(string title, string icon, string description, string milestoneTarget = "Milestone 4+")
    {
        Title = title;
        Icon = icon;
        Description = description;
        MilestoneTarget = milestoneTarget;
    }
}
