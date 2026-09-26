using InventoryPro.Domain.Entities;
using InventoryPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;

using InventoryPro.Application.Interfaces;

namespace InventoryPro.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    private IGenericRepository<Product>? _products;
    private IGenericRepository<Category>? _categories;
    private IGenericRepository<Customer>? _customers;
    private IGenericRepository<Order>? _orders;
    private IGenericRepository<OrderItem>? _orderItems;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IGenericRepository<Product> Products => _products ??= new GenericRepository<Product>(_context);
    public IGenericRepository<Category> Categories => _categories ??= new GenericRepository<Category>(_context);
    public IGenericRepository<Customer> Customers => _customers ??= new GenericRepository<Customer>(_context);
    public IGenericRepository<Order> Orders => _orders ??= new GenericRepository<Order>(_context);
    public IGenericRepository<OrderItem> OrderItems => _orderItems ??= new GenericRepository<OrderItem>(_context);

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public async Task<IDbContextTransaction> BeginTransactionAsync()
        => await _context.Database.BeginTransactionAsync();

    public void Dispose() => _context.Dispose();
}
