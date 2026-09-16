using RestaurantManagement.Application.Billing.DTOs;

namespace RestaurantManagement.Application.Billing.Interfaces;

/// <summary>
/// Service contract for generating bills, calculating totals/balances, and applying discounts.
/// </summary>
public interface IBillingService
{
    Task<BillDto?> GetBillForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillSummaryDto>> GetOrdersAwaitingBillingAsync(CancellationToken cancellationToken = default);

    Task<BillDto> ApplyDiscountAsync(ApplyDiscountRequest request, CancellationToken cancellationToken = default);
}
