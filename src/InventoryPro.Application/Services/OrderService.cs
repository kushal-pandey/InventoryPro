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

    public async Task<IEnumerable<OrderDto>> GetAllAsync()
    {
        var orders = await _uow.Orders.Query()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return _mapper.Map<IEnumerable<OrderDto>>(orders);
    }

    public async Task<OrderDto> GetByIdAsync(int id)
    {
        var order = await _uow.Orders.Query()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException(nameof(Order), id);

        return _mapper.Map<OrderDto>(order);
    }

    /// <summary>
    /// Creates an order, validates stock availability for every line item, and
    /// atomically decrements stock. Wrapped in a DB transaction so a failure on
    /// any line rolls back the whole order (no partial stock deductions).
    /// </summary>
    public async Task<OrderDto> CreateAsync(CreateOrderDto dto, string userId)
    {
        var customerExists = await _uow.Customers.Query().AnyAsync(c => c.Id == dto.CustomerId);
        if (!customerExists)
            throw new NotFoundException(nameof(Customer), dto.CustomerId);

        using var transaction = await _uow.BeginTransactionAsync();
        try
        {
            var order = new Order
            {
                OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{new Random().Next(100, 999)}",
                CustomerId = dto.CustomerId,
                Status = OrderStatus.Pending,
                CreatedByUserId = userId,
                OrderDate = DateTime.UtcNow
            };

            decimal total = 0;

            foreach (var line in dto.Items)
            {
                var product = await _uow.Products.GetByIdAsync(line.ProductId)
                    ?? throw new NotFoundException(nameof(Product), line.ProductId);

                if (!product.IsActive)
                    throw new ConflictException($"Product '{product.Name}' is not currently available.");

                if (product.StockQuantity < line.Quantity)
                    throw new ConflictException(
                        $"Insufficient stock for '{product.Name}'. Available: {product.StockQuantity}, requested: {line.Quantity}.");

                product.StockQuantity -= line.Quantity;
                _uow.Products.Update(product);

                var orderItem = new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = line.Quantity,
                    UnitPrice = product.Price
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
        var order = await _uow.Orders.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Order), id);

        if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Delivered)
            throw new ConflictException($"Order in status '{order.Status}' cannot be modified further.");

        order.Status = dto.Status;
        _uow.Orders.Update(order);
        await _uow.SaveChangesAsync();

        return await GetByIdAsync(id);
    }
}
