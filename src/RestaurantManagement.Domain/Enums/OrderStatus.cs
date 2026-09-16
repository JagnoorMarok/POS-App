namespace RestaurantManagement.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a restaurant order.
/// </summary>
public enum OrderStatus
{
    Draft = 1,
    Confirmed = 2,
    Preparing = 3,
    Ready = 4,
    Served = 5,
    Completed = 6,
    Cancelled = 7
}
