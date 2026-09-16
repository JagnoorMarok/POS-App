using RestaurantManagement.Domain.Common;

namespace RestaurantManagement.Domain.Entities;

/// <summary>
/// Represents a physical dining table in the restaurant.
/// </summary>
public class RestaurantTable : Entity<Guid>, IAggregateRoot
{
    public string TableNumber { get; private set; } = default!;
    public int Capacity { get; private set; }
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private RestaurantTable()
    {
        // For EF Core
    }

    public RestaurantTable(
        Guid id,
        string tableNumber,
        int capacity,
        DateTime createdAt,
        int displayOrder = 0,
        bool isActive = true)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(tableNumber))
            throw new ArgumentException("Table number cannot be null or empty.", nameof(tableNumber));

        if (capacity <= 0)
            throw new ArgumentException("Table capacity must be greater than zero.", nameof(capacity));

        TableNumber = tableNumber.Trim();
        Capacity = capacity;
        DisplayOrder = displayOrder;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public void UpdateDetails(string tableNumber, int capacity, int displayOrder, DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(tableNumber))
            throw new ArgumentException("Table number cannot be null or empty.", nameof(tableNumber));

        if (capacity <= 0)
            throw new ArgumentException("Table capacity must be greater than zero.", nameof(capacity));

        TableNumber = tableNumber.Trim();
        Capacity = capacity;
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
