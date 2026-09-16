using RestaurantManagement.Domain.Common;

namespace RestaurantManagement.Domain.Entities;

/// <summary>
/// Represents an ordered line item, capturing historical product name and unit price snapshots.
/// </summary>
public class OrderItem : Entity<Guid>
{
    public Guid OrderId { get; private set; }
    public Guid? ProductId { get; private set; }
    public string ProductNameSnapshot { get; private set; } = default!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? Notes { get; private set; }

    public Order? Order { get; private set; }
    public Product? Product { get; private set; }

    private OrderItem()
    {
        // For EF Core
    }

    public OrderItem(
        Guid id,
        Guid orderId,
        Guid? productId,
        string productNameSnapshot,
        decimal unitPrice,
        int quantity,
        decimal discountAmount = 0m,
        decimal taxAmount = 0m,
        string? notes = null)
        : base(id)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderItem must be associated with a valid Order.", nameof(orderId));

        if (string.IsNullOrWhiteSpace(productNameSnapshot))
            throw new ArgumentException("Product name snapshot cannot be empty.", nameof(productNameSnapshot));

        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        if (discountAmount < 0)
            throw new ArgumentException("Discount amount cannot be negative.", nameof(discountAmount));

        if (taxAmount < 0)
            throw new ArgumentException("Tax amount cannot be negative.", nameof(taxAmount));

        OrderId = orderId;
        ProductId = productId;
        ProductNameSnapshot = productNameSnapshot.Trim();
        UnitPrice = unitPrice;
        Quantity = quantity;
        DiscountAmount = discountAmount;
        TaxAmount = taxAmount;
        Notes = notes?.Trim();

        RecalculateTotal();
    }

    public void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(newQuantity));

        Quantity = newQuantity;
        RecalculateTotal();
    }

    public void ApplyDiscount(decimal discountAmount)
    {
        if (discountAmount < 0)
            throw new ArgumentException("Discount cannot be negative.", nameof(discountAmount));

        DiscountAmount = discountAmount;
        RecalculateTotal();
    }

    public void SetTax(decimal taxAmount)
    {
        if (taxAmount < 0)
            throw new ArgumentException("Tax amount cannot be negative.", nameof(taxAmount));

        TaxAmount = taxAmount;
        RecalculateTotal();
    }

    public void SetNotes(string? notes)
    {
        Notes = notes?.Trim();
    }

    private void RecalculateTotal()
    {
        var baseTotal = UnitPrice * Quantity;
        TotalAmount = Math.Max(0m, baseTotal - DiscountAmount) + TaxAmount;
    }
}
