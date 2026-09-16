using RestaurantManagement.Domain.Common;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Domain.Entities;

/// <summary>
/// Represents a local restaurant employee account for authentication and role authorization.
/// </summary>
public class Employee : Entity<Guid>, IAggregateRoot
{
    public string Username { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public EmployeeRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    private Employee()
    {
        // For EF Core
    }

    public Employee(
        Guid id,
        string username,
        string displayName,
        string passwordHash,
        EmployeeRole role,
        DateTime createdAt,
        bool isActive = true)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be null or empty.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be null or empty.", nameof(passwordHash));

        Username = username.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = createdAt;
        IsActive = isActive;
    }

    public void UpdateDetails(string displayName, EmployeeRole role, DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be null or empty.", nameof(displayName));

        DisplayName = displayName.Trim();
        Role = role;
        UpdatedAt = updatedAt;
    }

    public void SetPasswordHash(string newPasswordHash, DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be null or empty.", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        UpdatedAt = updatedAt;
    }

    public void ChangeRole(EmployeeRole newRole, DateTime updatedAt)
    {
        Role = newRole;
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

    public void RecordLogin(DateTime loginTimestamp)
    {
        LastLoginAt = loginTimestamp;
    }
}
