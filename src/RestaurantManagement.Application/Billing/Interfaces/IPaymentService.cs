using RestaurantManagement.Application.Billing.DTOs;

namespace RestaurantManagement.Application.Billing.Interfaces;

/// <summary>
/// Service contract managing offline/local payment recording and order settlement.
/// </summary>
public interface IPaymentService
{
    Task<PaymentDto> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentDto>> GetPaymentsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<PaymentSummaryDto> GetPaymentSummaryForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task CompleteOrderSettlementAsync(Guid orderId, CancellationToken cancellationToken = default);
}
