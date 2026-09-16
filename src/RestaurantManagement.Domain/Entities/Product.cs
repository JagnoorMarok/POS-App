using RestaurantManagement.Domain.Common;

namespace RestaurantManagement.Domain.Entities;

/// <summary>
/// Represents an orderable menu product item.
/// </summary>
public class Product : Entity<Guid>, IAggregateRoot
{
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public bool IsAvailable { get; private set; }
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }
    public string? ImagePath { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public Category? Category { get; private set; }

    private Product()
    {
        // For EF Core
    }

    public Product(
        Guid id,
        Guid categoryId,
        string name,
        string? description,
        decimal price,
        DateTime createdAt,
        int displayOrder = 0,
        bool isAvailable = true,
        bool isActive = true,
        string? imagePath = null)
        : base(id)
    {
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Product must belong to a valid Category.", nameof(categoryId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be null or empty.", nameof(name));

        if (price < 0)
            throw new ArgumentException("Product price cannot be negative.", nameof(price));

        CategoryId = categoryId;
        Name = name.Trim();
        Description = description?.Trim();
        Price = price;
        DisplayOrder = displayOrder;
        IsAvailable = isAvailable;
        IsActive = isActive;
        ImagePath = imagePath?.Trim();
        CreatedAt = createdAt;
    }

    public void UpdatePrice(decimal newPrice, DateTime updatedAt)
    {
        if (newPrice < 0)
            throw new ArgumentException("Product price cannot be negative.", nameof(newPrice));

        Price = newPrice;
        UpdatedAt = updatedAt;
    }

    public void UpdateDetails(
        string name,
        string? description,
        Guid categoryId,
        int displayOrder,
        string? imagePath,
        DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be null or empty.", nameof(name));

        if (categoryId == Guid.Empty)
            throw new ArgumentException("Product must belong to a valid Category.", nameof(categoryId));

        Name = name.Trim();
        Description = description?.Trim();
        CategoryId = categoryId;
        DisplayOrder = displayOrder;
        ImagePath = imagePath?.Trim();
        UpdatedAt = updatedAt;
    }

    public void SetAvailability(bool isAvailable, DateTime updatedAt)
    {
        IsAvailable = isAvailable;
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
