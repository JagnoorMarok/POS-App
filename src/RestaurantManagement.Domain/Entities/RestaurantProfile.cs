using RestaurantManagement.Domain.Common;

namespace RestaurantManagement.Domain.Entities;

/// <summary>
/// Represents the business profile and general configuration of the restaurant.
/// </summary>
public class RestaurantProfile : Entity<Guid>, IAggregateRoot
{
    public string RestaurantName { get; private set; } = default!;
    public string? Address { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string CurrencyCode { get; private set; } = "INR";
    public string? GstOrTaxNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private RestaurantProfile()
    {
        // For EF Core
    }

    public RestaurantProfile(
        Guid id,
        string restaurantName,
        DateTime createdAt,
        string? address = null,
        string? phoneNumber = null,
        string currencyCode = "INR",
        string? gstOrTaxNumber = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(restaurantName))
            throw new ArgumentException("Restaurant name cannot be null or empty.", nameof(restaurantName));

        RestaurantName = restaurantName.Trim();
        Address = address?.Trim();
        PhoneNumber = phoneNumber?.Trim();
        CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "INR" : currencyCode.Trim();
        GstOrTaxNumber = gstOrTaxNumber?.Trim();
        CreatedAt = createdAt;
    }

    public void UpdateProfile(
        string restaurantName,
        string? address,
        string? phoneNumber,
        string currencyCode,
        string? gstOrTaxNumber,
        DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(restaurantName))
            throw new ArgumentException("Restaurant name cannot be null or empty.", nameof(restaurantName));

        RestaurantName = restaurantName.Trim();
        Address = address?.Trim();
        PhoneNumber = phoneNumber?.Trim();
        CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "INR" : currencyCode.Trim();
        GstOrTaxNumber = gstOrTaxNumber?.Trim();
        UpdatedAt = updatedAt;
    }
}
