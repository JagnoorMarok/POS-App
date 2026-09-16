using RestaurantManagement.Domain.Common;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Domain.Entities;

/// <summary>
/// Aggregate root representing a restaurant customer order.
/// </summary>
public class Order : Entity<Guid>, IAggregateRoot
{
    private readonly List<OrderItem> _items = new();
    private readonly List<Payment> _payments = new();

    public string OrderNumber { get; private set; } = default!;
    public Guid? RestaurantTableId { get; private set; }
    public OrderStatus Status { get; private set; }
    public OrderType OrderType { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public RestaurantTable? RestaurantTable { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    private Order()
    {
        // For EF Core
    }

    public Order(
        Guid id,
        string orderNumber,
        OrderType orderType,
        DateTime createdAt,
        Guid? restaurantTableId = null,
        string? notes = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new ArgumentException("Order number cannot be null or empty.", nameof(orderNumber));

        OrderNumber = orderNumber.Trim();
        OrderType = orderType;
        RestaurantTableId = restaurantTableId;
        Status = OrderStatus.Draft;
        Notes = notes?.Trim();
        CreatedAt = createdAt;
    }

    public OrderItem AddItem(
        Product product,
        int quantity,
        decimal discountAmount = 0m,
        decimal taxAmount = 0m,
        string? notes = null)
    {
        if (product is null)
            throw new ArgumentNullException(nameof(product));

        return AddItem(
            product.Id,
            product.Name,
            product.Price,
            quantity,
            discountAmount,
            taxAmount,
            notes);
    }

    public OrderItem AddItem(
        Guid? productId,
        string productNameSnapshot,
        decimal unitPrice,
        int quantity,
        decimal discountAmount = 0m,
        decimal taxAmount = 0m,
        string? notes = null)
    {
        if (Status == OrderStatus.Completed || Status == OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot add items to an order with status {Status}.");

        var item = new OrderItem(
            Guid.NewGuid(),
            Id,
            productId,
            productNameSnapshot,
            unitPrice,
            quantity,
            discountAmount,
            taxAmount,
            notes);

        _items.Add(item);
        RecalculateTotals();
        return item;
    }

    public void RemoveItem(Guid orderItemId)
    {
        if (Status == OrderStatus.Completed || Status == OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot remove items from an order with status {Status}.");

        _items.RemoveAll(i => i.Id == orderItemId);
        RecalculateTotals();
    }

    public void UpdateItemQuantity(Guid orderItemId, int newQuantity)
    {
        if (Status == OrderStatus.Completed || Status == OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot modify items in an order with status {Status}.");

        var item = _items.FirstOrDefault(i => i.Id == orderItemId);
        if (item == null)
            throw new InvalidOperationException($"OrderItem with ID {orderItemId} was not found in Order {OrderNumber}.");

        item.UpdateQuantity(newQuantity);
        RecalculateTotals();
    }

    public void UpdateItemNotes(Guid orderItemId, string? notes)
    {
        if (Status == OrderStatus.Completed || Status == OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot modify item notes in an order with status {Status}.");

        var item = _items.FirstOrDefault(i => i.Id == orderItemId);
        if (item == null)
            throw new InvalidOperationException($"OrderItem with ID {orderItemId} was not found in Order {OrderNumber}.");

        item.SetNotes(notes);
    }

    public void UpdateOrderDetails(
        OrderType orderType,
        Guid? restaurantTableId,
        string? notes,
        decimal discountAmount,
        DateTime updatedAt)
    {
        if (Status == OrderStatus.Completed || Status == OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot update details of an order with status {Status}.");

        if (orderType == OrderType.DineIn && !restaurantTableId.HasValue)
            throw new ArgumentException("DineIn orders require a valid restaurant table.", nameof(restaurantTableId));

        if (orderType != OrderType.DineIn)
        {
            restaurantTableId = null; // Clear table for Takeaway / Delivery
        }

        if (discountAmount < 0)
            throw new ArgumentException("Discount amount cannot be negative.", nameof(discountAmount));

        OrderType = orderType;
        RestaurantTableId = restaurantTableId;
        Notes = notes?.Trim();
        DiscountAmount = discountAmount;
        UpdatedAt = updatedAt;
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        Subtotal = _items.Sum(i => i.UnitPrice * i.Quantity);
        var itemDiscounts = _items.Sum(i => i.DiscountAmount);
        DiscountAmount = DiscountAmount > 0 ? DiscountAmount : itemDiscounts;
        TaxAmount = _items.Sum(i => i.TaxAmount);
        TotalAmount = Math.Max(0m, Subtotal - DiscountAmount) + TaxAmount;
    }

    public void TransitionTo(OrderStatus newStatus, DateTime timestamp)
    {
        if (Status == newStatus)
            return;

        if (Status == OrderStatus.Completed)
            throw new InvalidOperationException($"Cannot transition completed order {OrderNumber} to {newStatus}.");

        if (Status == OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot transition cancelled order {OrderNumber} to {newStatus}.");

        // Validate state transitions
        var isValidTransition = (Status, newStatus) switch
        {
            (OrderStatus.Draft, OrderStatus.Confirmed) => true,
            (OrderStatus.Draft, OrderStatus.Completed) => true,
            (OrderStatus.Draft, OrderStatus.Cancelled) => true,
            (OrderStatus.Confirmed, OrderStatus.Preparing) => true,
            (OrderStatus.Confirmed, OrderStatus.Ready) => true,
            (OrderStatus.Confirmed, OrderStatus.Served) => true,
            (OrderStatus.Confirmed, OrderStatus.Completed) => true,
            (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
            (OrderStatus.Preparing, OrderStatus.Ready) => true,
            (OrderStatus.Preparing, OrderStatus.Served) => true,
            (OrderStatus.Preparing, OrderStatus.Completed) => true,
            (OrderStatus.Preparing, OrderStatus.Cancelled) => true,
            (OrderStatus.Ready, OrderStatus.Served) => true,
            (OrderStatus.Ready, OrderStatus.Completed) => true,
            (OrderStatus.Ready, OrderStatus.Cancelled) => true,
            (OrderStatus.Served, OrderStatus.Completed) => true,
            (OrderStatus.Served, OrderStatus.Cancelled) => true,
            _ => false
        };

        if (!isValidTransition)
            throw new InvalidOperationException($"Invalid order state transition from {Status} to {newStatus} for order {OrderNumber}.");

        Status = newStatus;
        UpdatedAt = timestamp;

        if (newStatus == OrderStatus.Completed)
        {
            CompletedAt = timestamp;
        }
    }

    public void Cancel(DateTime timestamp, string? reason = null)
    {
        TransitionTo(OrderStatus.Cancelled, timestamp);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Notes = string.IsNullOrWhiteSpace(Notes) ? $"Cancelled: {reason}" : $"{Notes} | Cancelled: {reason}";
        }
    }
}
