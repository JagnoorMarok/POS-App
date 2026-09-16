namespace RestaurantManagement.Application.Common.Interfaces;

/// <summary>
/// Abstraction for providing current date and time to decouple from system clock in business logic and testing.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}
