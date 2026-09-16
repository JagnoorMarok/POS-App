using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Kitchen.DTOs;
using RestaurantManagement.Application.Kitchen.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;

namespace RestaurantManagement.Infrastructure.Services;

public class KitchenService : IKitchenService
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<KitchenService> _logger;

    public KitchenService(
        RestaurantDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        ILogger<KitchenService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KitchenOrderDto>> GetKitchenOrdersAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        var orders = await _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Items)
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Confirmed ||
                        o.Status == OrderStatus.Preparing ||
                        o.Status == OrderStatus.Ready)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(MapToKitchenDto).ToList();
    }

    public async Task<KitchenOrderDto?> GetKitchenOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        var order = await _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        return order == null ? null : MapToKitchenDto(order);
    }

    public async Task<KitchenOrderDto> StartPreparationAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        var order = await _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        if (order.Status != OrderStatus.Confirmed)
        {
            throw new ValidationException($"Cannot start preparation for order {order.OrderNumber} with status {order.Status}. Order must be in Confirmed status.");
        }

        order.TransitionTo(OrderStatus.Preparing, _dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Kitchen started preparation for order {OrderNumber} (Staff: {Staff})",
            order.OrderNumber, _currentUserService.Username ?? "Unknown");

        return MapToKitchenDto(order);
    }

    public async Task<KitchenOrderDto> MarkOrderReadyAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        var order = await _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        if (order.Status != OrderStatus.Confirmed && order.Status != OrderStatus.Preparing)
        {
            throw new ValidationException($"Cannot mark order {order.OrderNumber} as ready from status {order.Status}.");
        }

        order.TransitionTo(OrderStatus.Ready, _dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Kitchen marked order {OrderNumber} as Ready for service (Staff: {Staff})",
            order.OrderNumber, _currentUserService.Username ?? "Unknown");

        return MapToKitchenDto(order);
    }

    public async Task<KitchenOrderDto> MarkOrderServedAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized();

        var order = await _dbContext.Orders
            .Include(o => o.RestaurantTable)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException(nameof(Order), orderId);
        }

        if (order.Status != OrderStatus.Confirmed &&
            order.Status != OrderStatus.Preparing &&
            order.Status != OrderStatus.Ready)
        {
            throw new ValidationException($"Cannot mark order {order.OrderNumber} as served from status {order.Status}.");
        }

        order.TransitionTo(OrderStatus.Served, _dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderNumber} marked as Served (Staff: {Staff})",
            order.OrderNumber, _currentUserService.Username ?? "Unknown");

        return MapToKitchenDto(order);
    }

    private void EnsureAuthorized()
    {
        // If current user is authenticated, check feature access
        if (_currentUserService.IsAuthenticated)
        {
            var isAllowed = _authorizationService.CanAccessFeature(_currentUserService.Role, "kitchen");
            if (!isAllowed)
            {
                throw new UnauthorizedAccessException($"Role '{_currentUserService.Role}' is not authorized to access kitchen operations.");
            }
        }
    }

    private static KitchenOrderDto MapToKitchenDto(Order o)
    {
        return new KitchenOrderDto(
            o.Id,
            o.OrderNumber,
            o.OrderType,
            o.Status,
            o.RestaurantTableId,
            o.RestaurantTable?.TableNumber,
            o.Notes,
            o.CreatedAt,
            o.UpdatedAt,
            o.Items.Select(i => new KitchenOrderItemDto(
                i.Id,
                i.ProductId,
                i.ProductNameSnapshot,
                i.Quantity,
                i.Notes)).ToList());
    }
}
