using RestaurantManagement.Domain.Common;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Domain.Entities;

/// <summary>
/// Represents a payment transaction against an order.
/// </summary>
public class Payment : Entity<Guid>, IAggregateRoot
{
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? TransactionReference { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public Order? Order { get; private set; }

    private Payment()
    {
        // For EF Core
    }

    public Payment(
        Guid id,
        Guid orderId,
        decimal amount,
        PaymentMethod paymentMethod,
        DateTime createdAt,
        PaymentStatus status = PaymentStatus.Pending,
        string? transactionReference = null)
        : base(id)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Payment must be associated with a valid Order.", nameof(orderId));

        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        OrderId = orderId;
        Amount = amount;
        PaymentMethod = paymentMethod;
        Status = status;
        TransactionReference = transactionReference?.Trim();
        CreatedAt = createdAt;
    }

    public void MarkCompleted(DateTime completedAt, string? transactionReference = null)
    {
        Status = PaymentStatus.Completed;
        CompletedAt = completedAt;
        if (!string.IsNullOrWhiteSpace(transactionReference))
        {
            TransactionReference = transactionReference.Trim();
        }
    }

    public void MarkFailed(DateTime failedAt, string? reason = null)
    {
        Status = PaymentStatus.Failed;
        CompletedAt = failedAt;
    }

    public void Refund(DateTime refundedAt)
    {
        Status = PaymentStatus.Refunded;
        CompletedAt = refundedAt;
    }

    public void Cancel(DateTime cancelledAt)
    {
        Status = PaymentStatus.Cancelled;
        CompletedAt = cancelledAt;
    }
}
