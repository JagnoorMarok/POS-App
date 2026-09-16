using RestaurantManagement.Domain.Common;

namespace RestaurantManagement.Domain.Entities;

/// <summary>
/// Persistent application setting entity used for system configuration and database infrastructure verification.
/// </summary>
public class ApplicationSetting : Entity<Guid>, IAggregateRoot
{
    public string Key { get; private set; } = default!;
    public string Value { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ApplicationSetting()
    {
        // For EF Core
    }

    public ApplicationSetting(Guid id, string key, string value, DateTime createdAt) : base(id)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be null or empty.", nameof(key));

        Key = key.Trim();
        Value = value ?? string.Empty;
        CreatedAt = createdAt;
    }

    public void UpdateValue(string newValue, DateTime updatedAt)
    {
        Value = newValue ?? string.Empty;
        UpdatedAt = updatedAt;
    }
}
