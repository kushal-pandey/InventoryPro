using AutoMapper;
using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;
using InventoryPro.Application.Interfaces;
using InventoryPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryPro.Application.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public OrderService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<PagedResult<OrderDto>> GetAllAsync(ListQueryParameters query)
    {
        var source = _uow.Orders.Query()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .AsNoTracking()
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id);

        var totalCount = await source.CountAsync();
        var pageNumber = query.SafePageNumber;
        var pageSize = query.SafePageSize;
        var orders = await source.Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<OrderDto>
        {
            Items = _mapper.Map<List<OrderDto>>(orders),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<OrderDto> GetByIdAsync(int id)
    {
        var order = await _uow.Orders.Query()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException(nameof(Order), id);

        return _mapper.Map<OrderDto>(order);
    }

    /// <summary>
    /// Uses a conditional database-side stock decrement, so concurrent requests
    /// cannot both sell the same final units. The enclosing transaction rolls back
    /// all prior line decrements if any line fails.
    /// </summary>
    public async Task<OrderDto> CreateAsync(CreateOrderDto dto, string userId)
    {
        var customerExists = await _uow.Customers.Query().AnyAsync(c => c.Id == dto.CustomerId);
        if (!customerExists)
            throw new NotFoundException(nameof(Customer), dto.CustomerId);

        await using var transaction = await _uow.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var order = new Order
            {
                OrderNumber = $"ORD-{Guid.NewGuid():N}"[..30],
                CustomerId = dto.CustomerId,
                Status = OrderStatus.Pending,
                CreatedByUserId = userId,
                OrderDate = now
            };

            decimal total = 0;

            // Combining duplicate product lines first prevents a request from
            // independently passing stock checks for the same product.
            var lines = dto.Items
                .GroupBy(item => item.ProductId)
                .Select(group => new
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(item => item.Quantity)
                })
                .ToList();

            foreach (var line in lines)
            {
                var productInfo = await _uow.Products.Query()
                    .AsNoTracking()
                    .Where(p => p.Id == line.ProductId)
                    .Select(p => new { p.Id, p.Name, p.Price, p.IsActive })
                    .FirstOrDefaultAsync();

                if (productInfo is null)
                    throw new NotFoundException(nameof(Product), line.ProductId);

                if (!productInfo.IsActive)
                    throw new ConflictException($"Product '{productInfo.Name}' is not currently available.");

                var affected = await _uow.Products.Query()
                    .Where(p => p.Id == line.ProductId &&
                                p.IsActive &&
                                p.StockQuantity >= line.Quantity)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(p => p.StockQuantity, p => p.StockQuantity - line.Quantity));

                if (affected == 0)
                {
                    var available = await _uow.Products.Query()
                        .AsNoTracking()
                        .Where(p => p.Id == line.ProductId)
                        .Select(p => (int?)p.StockQuantity)
                        .FirstOrDefaultAsync();

                    if (available is null)
                        throw new NotFoundException(nameof(Product), line.ProductId);

                    throw new ConflictException(
                        $"Insufficient stock for '{productInfo.Name}'. Available: {available}, requested: {line.Quantity}.");
                }

                var orderItem = new OrderItem
                {
                    ProductId = productInfo.Id,
                    Quantity = line.Quantity,
                    UnitPrice = productInfo.Price
                };
                total += orderItem.Quantity * orderItem.UnitPrice;
                order.OrderItems.Add(orderItem);
            }

            order.TotalAmount = total;
            await _uow.Orders.AddAsync(order);
            await _uow.SaveChangesAsync();
            await transaction.CommitAsync();

            return await GetByIdAsync(order.Id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<OrderDto> UpdateStatusAsync(int id, UpdateOrderStatusDto dto)
    {
        if (!Enum.IsDefined(dto.Status))
            throw new ConflictException("The requested order status is invalid.");

        await using var transaction = await _uow.BeginTransactionAsync();
        try
        {
            var order = await _uow.Orders.Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id)
                ?? throw new NotFoundException(nameof(Order), id);

            if (order.Status is OrderStatus.Cancelled or OrderStatus.Delivered)
                throw new ConflictException($"Order in status '{order.Status}' cannot be modified further.");

            if (dto.Status == order.Status)
                return await GetByIdAsync(id);

            var transitionAllowed = order.Status switch
            {
                OrderStatus.Pending => dto.Status is OrderStatus.Confirmed or OrderStatus.Cancelled,
                OrderStatus.Confirmed => dto.Status is OrderStatus.Shipped or OrderStatus.Cancelled,
                OrderStatus.Shipped => dto.Status == OrderStatus.Delivered,
                _ => false
            };

            if (!transitionAllowed)
                throw new ConflictException(
                    $"Order status cannot transition from '{order.Status}' to '{dto.Status}'.");

            // Compare-and-set status protects cancellation from duplicate restocking
            // when two requests attempt to cancel the same order concurrently.
            var changed = await _uow.Orders.Query()
                .Where(o => o.Id == id && o.Status == order.Status)
                .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.Status, dto.Status));

            if (changed == 0)
                throw new ConflictException("The order status changed concurrently. Reload the order and try again.");

            if (dto.Status == OrderStatus.Cancelled)
            {
                var items = await _uow.OrderItems.Query()
                    .AsNoTracking()
                    .Where(item => item.OrderId == id)
                    .ToListAsync();

                foreach (var item in items)
                {
                    var restored = await _uow.Products.Query()
                        .Where(p => p.Id == item.ProductId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity));

                    if (restored == 0)
                        throw new ConflictException($"Unable to restore stock for product {item.ProductId}.");
                }
            }

            await transaction.CommitAsync();
            return await GetByIdAsync(id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
