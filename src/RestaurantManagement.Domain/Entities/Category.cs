using RestaurantManagement.Domain.Common;

namespace RestaurantManagement.Domain.Entities;

/// <summary>
/// Represents a menu category in the restaurant (e.g. Starters, Main Course, Beverages).
/// </summary>
public class Category : Entity<Guid>, IAggregateRoot
{
    private readonly List<Product> _products = new();

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyCollection<Product> Products => _products.AsReadOnly();

    private Category()
    {
        // For EF Core
    }

    public Category(
        Guid id,
        string name,
        string? description,
        int displayOrder,
        DateTime createdAt,
        bool isActive = true)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name cannot be null or empty.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        DisplayOrder = displayOrder;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public void UpdateDetails(string name, string? description, int displayOrder, DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name cannot be null or empty.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        DisplayOrder = displayOrder;
        UpdatedAt = updatedAt;
    }

    public void Deactivate(DateTime updatedAt)
    {
        IsActive = false;
        UpdatedAt = updatedAt;
    }

    public void Activate(DateTime updatedAt)
    {
        IsActive = true;
        UpdatedAt = updatedAt;
    }
}
