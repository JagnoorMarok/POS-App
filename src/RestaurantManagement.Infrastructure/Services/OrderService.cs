using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Orders.DTOs;
using RestaurantManagement.Application.Orders.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;

namespace RestaurantManagement.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        RestaurantDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<OrderService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(
        OrderStatus? status = null,
        bool onlyOpen = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Items)
            .AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }
        else if (onlyOpen)
        {
            query = query.Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order == null ? null : MapToDto(order);
    }

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        var normalized = orderNumber.Trim();
        var order = await _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderNumber == normalized, cancellationToken);

        return order == null ? null : MapToDto(order);
    }

    public async Task<OrderDto?> GetActiveOrderByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.RestaurantTableId == tableId &&
                                      o.Status != OrderStatus.Completed &&
                                      o.Status != OrderStatus.Cancelled, cancellationToken);

        return order == null ? null : MapToDto(order);
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OrderType == OrderType.DineIn)
        {
            if (!request.RestaurantTableId.HasValue)
            {
                throw new ValidationException(nameof(request.RestaurantTableId), "Dine-in orders require a table selection.");
            }

            var table = await _dbContext.RestaurantTables
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.RestaurantTableId.Value, cancellationToken);

            if (table == null)
            {
                throw new NotFoundException(nameof(RestaurantTable), request.RestaurantTableId.Value);
            }

            if (!table.IsActive)
            {
                throw new ValidationException(nameof(request.RestaurantTableId), $"Table '{table.TableNumber}' is inactive.");
            }

            // Check if table is occupied by another active order
            var isOccupied = await _dbContext.Orders
                .AnyAsync(o => o.RestaurantTableId == request.RestaurantTableId.Value &&
                               o.Status != OrderStatus.Completed &&
                               o.Status != OrderStatus.Cancelled, cancellationToken);

            if (isOccupied)
            {
                throw new ValidationException(nameof(request.RestaurantTableId), $"Table '{table.TableNumber}' already has an active order.");
            }
        }

        var orderNumber = await GenerateNextOrderNumberAsync(cancellationToken);
        var now = _dateTimeProvider.UtcNow;

        var order = new Order(
            Guid.NewGuid(),
            orderNumber,
            request.OrderType,
            now,
            request.OrderType == OrderType.DineIn ? request.RestaurantTableId : null,
            request.Notes);

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created draft order {OrderNumber} (Type: {OrderType})", order.OrderNumber, order.OrderType);

        return await GetOrderByIdAsync(order.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve created order.");
    }

    public async Task<OrderDto> AddItemToOrderAsync(Guid orderId, AddOrderItemRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero.");
        }

        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
        {
            throw new ValidationException($"Cannot add items to an order in {order.Status} status.");
        }

        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException(nameof(Product), request.ProductId);
        }

        if (!product.IsActive)
        {
            throw new ValidationException(nameof(request.ProductId), $"Product '{product.Name}' is inactive and cannot be ordered.");
        }

        if (!product.IsAvailable)
        {
            throw new ValidationException(nameof(request.ProductId), $"Product '{product.Name}' is currently marked out-of-stock.");
        }

        var taxRate = await GetTaxRatePercentAsync(cancellationToken);

        // Check if an existing line item for this product without special notes already exists to consolidate quantity
        var existingItem = order.Items.FirstOrDefault(i =>
            i.ProductId == product.Id &&
            string.Equals(i.Notes, request.Notes?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (existingItem != null)
        {
            var newQuantity = existingItem.Quantity + request.Quantity;
            existingItem.UpdateQuantity(newQuantity);
            var itemTax = Math.Round((existingItem.UnitPrice * newQuantity - existingItem.DiscountAmount) * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
            existingItem.SetTax(itemTax);
            order.RecalculateTotals();
        }
        else
        {
            // Calculate tax amount based on default restaurant tax rate
            var itemTax = Math.Round((product.Price * request.Quantity) * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);

            // Snapshot current Product.Name and Product.Price
            var newItem = order.AddItem(
                product.Id,
                product.Name,
                product.Price,
                request.Quantity,
                discountAmount: 0m,
                taxAmount: itemTax,
                notes: request.Notes);

            _dbContext.OrderItems.Add(newItem);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Added product '{ProductName}' (Qty: {Quantity}, Tax: {Tax}) to order {OrderNumber}", product.Name, request.Quantity, order.TaxAmount, order.OrderNumber);

        return await GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload order after adding item.");
    }

    public async Task<OrderDto> UpdateOrderItemQuantityAsync(Guid orderId, UpdateOrderItemQuantityRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero.");
        }

        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        var taxRate = await GetTaxRatePercentAsync(cancellationToken);
        var item = order.Items.FirstOrDefault(i => i.Id == request.OrderItemId);
        if (item != null)
        {
            item.UpdateQuantity(request.Quantity);
            var itemTax = Math.Round((item.UnitPrice * request.Quantity - item.DiscountAmount) * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
            item.SetTax(itemTax);
            order.RecalculateTotals();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload order.");
    }

    public async Task<OrderDto> UpdateOrderItemNotesAsync(Guid orderId, UpdateOrderItemNotesRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        order.UpdateItemNotes(request.OrderItemId, request.Notes);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload order.");
    }

    public async Task<OrderDto> RemoveOrderItemAsync(Guid orderId, Guid orderItemId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
        if (item != null)
        {
            order.RemoveItem(orderItemId);
            _dbContext.OrderItems.Remove(item);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload order.");
    }

    public async Task<OrderDto> UpdateOrderDetailsAsync(Guid orderId, UpdateOrderDetailsRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        if (request.OrderType == OrderType.DineIn)
        {
            if (!request.RestaurantTableId.HasValue)
            {
                throw new ValidationException(nameof(request.RestaurantTableId), "Dine-in orders require a table selection.");
            }

            var table = await _dbContext.RestaurantTables
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.RestaurantTableId.Value, cancellationToken);

            if (table == null)
            {
                throw new NotFoundException(nameof(RestaurantTable), request.RestaurantTableId.Value);
            }

            if (!table.IsActive)
            {
                throw new ValidationException(nameof(request.RestaurantTableId), $"Table '{table.TableNumber}' is inactive.");
            }

            // Check conflict if table changed
            if (order.RestaurantTableId != request.RestaurantTableId)
            {
                var isOccupied = await _dbContext.Orders
                    .AnyAsync(o => o.Id != orderId &&
                                   o.RestaurantTableId == request.RestaurantTableId.Value &&
                                   o.Status != OrderStatus.Completed &&
                                   o.Status != OrderStatus.Cancelled, cancellationToken);

                if (isOccupied)
                {
                    throw new ValidationException(nameof(request.RestaurantTableId), $"Table '{table.TableNumber}' already has another active order.");
                }
            }
        }

        var now = _dateTimeProvider.UtcNow;
        order.UpdateOrderDetails(
            request.OrderType,
            request.OrderType == OrderType.DineIn ? request.RestaurantTableId : null,
            request.Notes,
            request.DiscountAmount,
            now);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload order.");
    }

    public async Task<OrderDto> ActivateOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        if (!order.Items.Any())
        {
            throw new ValidationException("Cannot activate an order with no items.");
        }

        order.TransitionTo(OrderStatus.Confirmed, _dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Order {OrderNumber} activated/confirmed.", order.OrderNumber);

        return await GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload order.");
    }

    public async Task<OrderDto> CancelOrderAsync(Guid orderId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        order.Cancel(_dateTimeProvider.UtcNow, reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Order {OrderNumber} cancelled. Reason: {Reason}", order.OrderNumber, reason ?? "None");

        return await GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload order.");
    }

    public async Task<OrderDto> CompleteOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        order.TransitionTo(OrderStatus.Completed, _dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Order {OrderNumber} marked completed.", order.OrderNumber);

        return await GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload order.");
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> GetTableOccupancyMapAsync(CancellationToken cancellationToken = default)
    {
        var occupiedTableIds = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.RestaurantTableId.HasValue &&
                        o.Status != OrderStatus.Completed &&
                        o.Status != OrderStatus.Cancelled)
            .Select(o => o.RestaurantTableId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var allTables = await _dbContext.RestaurantTables
            .AsNoTracking()
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var map = allTables.ToDictionary(id => id, id => occupiedTableIds.Contains(id));
        return map;
    }

    private async Task<decimal> GetTaxRatePercentAsync(CancellationToken cancellationToken)
    {
        var taxRateSetting = await _dbContext.ApplicationSettings
            .FirstOrDefaultAsync(s => s.Key == "app_default_tax_rate", cancellationToken);
        if (taxRateSetting != null && decimal.TryParse(taxRateSetting.Value, out var parsedTax))
        {
            return parsedTax;
        }
        return 5.0m;
    }

    private async Task<string> GenerateNextOrderNumberAsync(CancellationToken cancellationToken)
    {
        var totalCount = await _dbContext.Orders.CountAsync(cancellationToken);
        var candidateNum = totalCount + 1;
        var candidate = $"ORD-{candidateNum:D6}";

        while (await _dbContext.Orders.AnyAsync(o => o.OrderNumber == candidate, cancellationToken))
        {
            candidateNum++;
            candidate = $"ORD-{candidateNum:D6}";
        }

        return candidate;
    }

    private static OrderDto MapToDto(Order o)
    {
        return new OrderDto(
            o.Id,
            o.OrderNumber,
            o.OrderType,
            o.Status,
            o.RestaurantTableId,
            o.RestaurantTable?.TableNumber,
            o.Subtotal,
            o.DiscountAmount,
            o.TaxAmount,
            o.TotalAmount,
            o.Notes,
            o.CreatedAt,
            o.UpdatedAt,
            o.CompletedAt,
            o.Items.Select(i => new OrderItemDto(
                i.Id,
                i.ProductId,
                i.ProductNameSnapshot,
                i.UnitPrice,
                i.Quantity,
                i.DiscountAmount,
                i.TaxAmount,
                i.TotalAmount,
                i.Notes)).ToList());
    }
}
