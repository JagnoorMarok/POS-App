using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Tables.DTOs;
using RestaurantManagement.Application.Tables.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;

namespace RestaurantManagement.Infrastructure.Services;

public class TableService : ITableService
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<TableService> _logger;

    public TableService(
        RestaurantDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<TableService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RestaurantTableDto>> GetAllTablesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RestaurantTables.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        var tables = await query
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.TableNumber)
            .Select(t => new
            {
                t.Id,
                t.TableNumber,
                t.Capacity,
                t.DisplayOrder,
                t.IsActive,
                t.CreatedAt,
                t.UpdatedAt,
                IsOccupied = _dbContext.Orders.Any(o =>
                    o.RestaurantTableId == t.Id &&
                    o.Status != OrderStatus.Completed &&
                    o.Status != OrderStatus.Cancelled)
            })
            .ToListAsync(cancellationToken);

        return tables.Select(t => new RestaurantTableDto(
            t.Id,
            t.TableNumber,
            t.Capacity,
            t.DisplayOrder,
            t.IsOccupied,
            t.IsActive,
            t.CreatedAt,
            t.UpdatedAt)).ToList();
    }

    public async Task<IReadOnlyList<RestaurantTableDto>> GetActiveTablesAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllTablesAsync(includeInactive: false, cancellationToken);
    }

    public async Task<RestaurantTableDto?> GetTableByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var table = await _dbContext.RestaurantTables
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.TableNumber,
                t.Capacity,
                t.DisplayOrder,
                t.IsActive,
                t.CreatedAt,
                t.UpdatedAt,
                IsOccupied = _dbContext.Orders.Any(o =>
                    o.RestaurantTableId == t.Id &&
                    o.Status != OrderStatus.Completed &&
                    o.Status != OrderStatus.Cancelled)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (table == null)
        {
            return null;
        }

        return new RestaurantTableDto(
            table.Id,
            table.TableNumber,
            table.Capacity,
            table.DisplayOrder,
            table.IsOccupied,
            table.IsActive,
            table.CreatedAt,
            table.UpdatedAt);
    }

    public async Task<RestaurantTableDto> CreateTableAsync(CreateTableRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTableRequest(request.TableNumber, request.Capacity);

        var trimmedTableNumber = request.TableNumber.Trim();

        var tableExists = await _dbContext.RestaurantTables
            .AnyAsync(t => t.TableNumber.ToLower() == trimmedTableNumber.ToLower() && t.IsActive, cancellationToken);

        if (tableExists)
        {
            throw new ValidationException(nameof(request.TableNumber), $"An active table with number '{trimmedTableNumber}' already exists.");
        }

        var now = _dateTimeProvider.UtcNow;
        var table = new RestaurantTable(
            Guid.NewGuid(),
            trimmedTableNumber,
            request.Capacity,
            now,
            request.DisplayOrder,
            request.IsActive);

        _dbContext.RestaurantTables.Add(table);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created table '{TableNumber}' (Capacity: {Capacity}, ID: {TableId})", table.TableNumber, table.Capacity, table.Id);

        return new RestaurantTableDto(
            table.Id,
            table.TableNumber,
            table.Capacity,
            table.DisplayOrder,
            false,
            table.IsActive,
            table.CreatedAt,
            table.UpdatedAt);
    }

    public async Task<RestaurantTableDto> UpdateTableAsync(UpdateTableRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTableRequest(request.TableNumber, request.Capacity);

        var table = await _dbContext.RestaurantTables.FindAsync(new object[] { request.Id }, cancellationToken);
        if (table == null)
        {
            throw new NotFoundException(nameof(RestaurantTable), request.Id);
        }

        var trimmedTableNumber = request.TableNumber.Trim();

        var tableExists = await _dbContext.RestaurantTables
            .AnyAsync(t => t.TableNumber.ToLower() == trimmedTableNumber.ToLower() && t.IsActive && t.Id != request.Id, cancellationToken);

        if (tableExists)
        {
            throw new ValidationException(nameof(request.TableNumber), $"An active table with number '{trimmedTableNumber}' already exists.");
        }

        var now = _dateTimeProvider.UtcNow;
        table.UpdateDetails(trimmedTableNumber, request.Capacity, request.DisplayOrder, now);

        if (request.IsActive && !table.IsActive)
        {
            table.Activate(now);
        }
        else if (!request.IsActive && table.IsActive)
        {
            table.Deactivate(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated table '{TableNumber}' (ID: {TableId})", table.TableNumber, table.Id);

        var isOccupied = await _dbContext.Orders.AnyAsync(o =>
            o.RestaurantTableId == table.Id &&
            o.Status != OrderStatus.Completed &&
            o.Status != OrderStatus.Cancelled, cancellationToken);

        return new RestaurantTableDto(
            table.Id,
            table.TableNumber,
            table.Capacity,
            table.DisplayOrder,
            isOccupied,
            table.IsActive,
            table.CreatedAt,
            table.UpdatedAt);
    }

    public async Task DeactivateTableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var table = await _dbContext.RestaurantTables.FindAsync(new object[] { id }, cancellationToken);
        if (table == null)
        {
            throw new NotFoundException(nameof(RestaurantTable), id);
        }

        table.Deactivate(_dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deactivated table (ID: {TableId})", id);
    }

    public async Task ActivateTableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var table = await _dbContext.RestaurantTables.FindAsync(new object[] { id }, cancellationToken);
        if (table == null)
        {
            throw new NotFoundException(nameof(RestaurantTable), id);
        }

        table.Activate(_dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Activated table (ID: {TableId})", id);
    }

    public async Task SetTableOccupancyAsync(Guid id, bool isOccupied, CancellationToken cancellationToken = default)
    {
        var table = await _dbContext.RestaurantTables.FindAsync(new object[] { id }, cancellationToken);
        if (table == null)
        {
            throw new NotFoundException(nameof(RestaurantTable), id);
        }

        var activeOrders = await _dbContext.Orders
            .Where(o => o.RestaurantTableId == id && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .ToListAsync(cancellationToken);

        if (isOccupied && !activeOrders.Any())
        {
            // Open a seated/draft dine-in order for the table
            var orderNumber = $"ORD-T{table.TableNumber}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var order = new Order(Guid.NewGuid(), orderNumber, OrderType.DineIn, _dateTimeProvider.UtcNow, table.Id);
            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Marked table '{TableNumber}' as occupied with order '{OrderNumber}'", table.TableNumber, orderNumber);
        }
        else if (!isOccupied && activeOrders.Any())
        {
            // Close active orders on table to clear occupancy
            foreach (var order in activeOrders)
            {
                order.Cancel(_dateTimeProvider.UtcNow, "Table marked unoccupied/cleared");
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Cleared occupancy for table '{TableNumber}'", table.TableNumber);
        }
    }

    public async Task DeleteTableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var table = await _dbContext.RestaurantTables.FindAsync(new object[] { id }, cancellationToken);
        if (table == null)
        {
            throw new NotFoundException(nameof(RestaurantTable), id);
        }

        var hasActiveOrders = await _dbContext.Orders
            .AnyAsync(o => o.RestaurantTableId == id && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled, cancellationToken);

        if (hasActiveOrders)
        {
            throw new ValidationException(nameof(id), $"Cannot delete Table '{table.TableNumber}' because it has active orders. Please complete or cancel the orders first.");
        }

        // Nullify table references in past completed/cancelled orders
        var pastOrders = await _dbContext.Orders
            .Where(o => o.RestaurantTableId == id)
            .ToListAsync(cancellationToken);

        foreach (var order in pastOrders)
        {
            order.UpdateOrderDetails(order.OrderType, null, order.Notes, order.DiscountAmount, _dateTimeProvider.UtcNow);
        }

        _dbContext.RestaurantTables.Remove(table);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted table '{TableNumber}' (ID: {TableId})", table.TableNumber, id);
    }


    private static void ValidateTableRequest(string tableNumber, int capacity)
    {
        if (string.IsNullOrWhiteSpace(tableNumber))
        {
            throw new ValidationException(nameof(tableNumber), "Table number is required.");
        }

        if (tableNumber.Trim().Length > 20)
        {
            throw new ValidationException(nameof(tableNumber), "Table number cannot exceed 20 characters.");
        }

        if (capacity <= 0)
        {
            throw new ValidationException(nameof(capacity), "Table capacity must be greater than zero.");
        }
    }
}
