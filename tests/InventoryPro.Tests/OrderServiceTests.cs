
using AutoMapper;
using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;
using InventoryPro.Application.Mappings;
using InventoryPro.Application.Services;
using InventoryPro.Domain.Entities;
using InventoryPro.Infrastructure.Data;
using InventoryPro.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryPro.Tests;

public sealed class OrderServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private OrderService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        var configuration = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance);

        var mapper = configuration.CreateMapper();
        var unitOfWork = new UnitOfWork(_context);

        _service = new OrderService(unitOfWork, mapper);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<Customer> AddCustomerAsync()
    {
        var customer = new Customer
        {
            FullName = "Test Customer",
            Email = "customer@example.com"
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return customer;
    }

    private async Task<Product> AddProductAsync(
        string name = "Keyboard",
        string sku = "KB-001",
        decimal price = 1500m,
        int stock = 20,
        bool isActive = true)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync();

        if (category is null)
        {
            category = new Category
            {
                Name = "Electronics",
                Description = "Electronic products"
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
        }

        var product = new Product
        {
            Name = name,
            Sku = sku,
            Price = price,
            StockQuantity = stock,
            ReorderLevel = 5,
            CategoryId = category.Id,
            Category = category,
            IsActive = isActive
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return product;
    }

    private static CreateOrderDto CreateOrderDto(
        int customerId,
        params CreateOrderItemDto[] items)
    {
        return new CreateOrderDto
        {
            CustomerId = customerId,
            Items = items.ToList()
        };
    }

    [Fact]
    public async Task CreateAsync_ValidOrder_CreatesOrderAndDeductsStock()
    {
        var customer = await AddCustomerAsync();
        var product = await AddProductAsync(stock: 20);

        var dto = CreateOrderDto(
            customer.Id,
            new CreateOrderItemDto
            {
                ProductId = product.Id,
                Quantity = 3
            });

        var result = await _service.CreateAsync(dto, "test-user");

        Assert.True(result.Id > 0);
        Assert.False(string.IsNullOrWhiteSpace(result.OrderNumber));
        Assert.Equal(customer.Id, result.CustomerId);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.Equal(4500m, result.TotalAmount);
        Assert.Single(result.Items);

        var persistedProduct = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == product.Id);

        Assert.Equal(17, persistedProduct.StockQuantity);

        var persistedOrder = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == result.Id);

        Assert.NotNull(persistedOrder);
        Assert.Equal("test-user", persistedOrder.CreatedByUserId);
    }

    [Fact]
    public async Task CreateAsync_MultipleItems_CalculatesTotalAndDeductsEachStock()
    {
        var customer = await AddCustomerAsync();

        var keyboard = await AddProductAsync(
            name: "Keyboard",
            sku: "KB-001",
            price: 1500m,
            stock: 20);

        var mouse = await AddProductAsync(
            name: "Mouse",
            sku: "MS-001",
            price: 500m,
            stock: 30);

        var dto = CreateOrderDto(
            customer.Id,
            new CreateOrderItemDto
            {
                ProductId = keyboard.Id,
                Quantity = 2
            },
            new CreateOrderItemDto
            {
                ProductId = mouse.Id,
                Quantity = 3
            });

        var result = await _service.CreateAsync(dto, "test-user");

        // (2 * 1500) + (3 * 500) = 4500
        Assert.Equal(4500m, result.TotalAmount);
        Assert.Equal(2, result.Items.Count);

        var keyboardInDb = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == keyboard.Id);

        var mouseInDb = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == mouse.Id);

        Assert.Equal(18, keyboardInDb.StockQuantity);
        Assert.Equal(27, mouseInDb.StockQuantity);
    }

    [Fact]
    public async Task CreateAsync_MissingCustomer_ThrowsNotFoundException()
    {
        var product = await AddProductAsync(stock: 20);

        var dto = CreateOrderDto(
            9999,
            new CreateOrderItemDto
            {
                ProductId = product.Id,
                Quantity = 1
            });

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateAsync(dto, "test-user"));

        Assert.Equal(404, exception.StatusCode);

        var persistedProduct = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == product.Id);

        Assert.Equal(20, persistedProduct.StockQuantity);

        Assert.Empty(await _context.Orders.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_MissingProduct_ThrowsNotFoundException()
    {
        var customer = await AddCustomerAsync();

        var dto = CreateOrderDto(
            customer.Id,
            new CreateOrderItemDto
            {
                ProductId = 9999,
                Quantity = 1
            });

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateAsync(dto, "test-user"));

        Assert.Equal(404, exception.StatusCode);
        Assert.Empty(await _context.Orders.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_InsufficientStock_ThrowsConflictException()
    {
        var customer = await AddCustomerAsync();
        var product = await AddProductAsync(stock: 2);

        var dto = CreateOrderDto(
            customer.Id,
            new CreateOrderItemDto
            {
                ProductId = product.Id,
                Quantity = 3
            });

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync(dto, "test-user"));

        Assert.Equal(409, exception.StatusCode);

        var persistedProduct = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == product.Id);

        Assert.Equal(2, persistedProduct.StockQuantity);
        Assert.Empty(await _context.Orders.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_InactiveProduct_ThrowsConflictException()
    {
        var customer = await AddCustomerAsync();
        var product = await AddProductAsync(
            stock: 20,
            isActive: false);

        var dto = CreateOrderDto(
            customer.Id,
            new CreateOrderItemDto
            {
                ProductId = product.Id,
                Quantity = 1
            });

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync(dto, "test-user"));

        Assert.Equal(409, exception.StatusCode);

        var persistedProduct = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == product.Id);

        Assert.Equal(20, persistedProduct.StockQuantity);
        Assert.Empty(await _context.Orders.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_LaterItemFails_DoesNotPersistPartialOrderOrStockChanges()
    {
        var customer = await AddCustomerAsync();

        var firstProduct = await AddProductAsync(
            name: "Keyboard",
            sku: "KB-001",
            stock: 20);

        // The first line is valid, but this product has insufficient stock.
        var secondProduct = await AddProductAsync(
            name: "Mouse",
            sku: "MS-001",
            stock: 1);

        var dto = CreateOrderDto(
            customer.Id,
            new CreateOrderItemDto
            {
                ProductId = firstProduct.Id,
                Quantity = 5
            },
            new CreateOrderItemDto
            {
                ProductId = secondProduct.Id,
                Quantity = 2
            });

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync(dto, "test-user"));

        // Read persisted values without using tracked entity instances.
        var firstProductInDb = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == firstProduct.Id);

        var secondProductInDb = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == secondProduct.Id);

        Assert.Equal(20, firstProductInDb.StockQuantity);
        Assert.Equal(1, secondProductInDb.StockQuantity);
        Assert.Empty(await _context.Orders.ToListAsync());
        Assert.Empty(await _context.OrderItems.ToListAsync());
    }

    [Fact]
    public async Task GetByIdAsync_MissingOrder_ThrowsNotFoundException()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(9999));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidOrder_UpdatesStatus()
    {
        var customer = await AddCustomerAsync();
        var product = await AddProductAsync();

        var created = await _service.CreateAsync(
            CreateOrderDto(
                customer.Id,
                new CreateOrderItemDto
                {
                    ProductId = product.Id,
                    Quantity = 1
                }),
            "test-user");

        var result = await _service.UpdateStatusAsync(
            created.Id,
            new UpdateOrderStatusDto
            {
                Status = OrderStatus.Confirmed
            });

        Assert.Equal(OrderStatus.Confirmed, result.Status);

        var persistedOrder = await _context.Orders
            .AsNoTracking()
            .FirstAsync(o => o.Id == created.Id);

        Assert.Equal(OrderStatus.Confirmed, persistedOrder.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_MissingOrder_ThrowsNotFoundException()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateStatusAsync(
                9999,
                new UpdateOrderStatusDto
                {
                    Status = OrderStatus.Confirmed
                }));

        Assert.Equal(404, exception.StatusCode);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Delivered)]
    public async Task UpdateStatusAsync_TerminalOrder_ThrowsConflictException(
        OrderStatus terminalStatus)
    {
        var customer = await AddCustomerAsync();
        var product = await AddProductAsync();

        var created = await _service.CreateAsync(
            CreateOrderDto(
                customer.Id,
                new CreateOrderItemDto
                {
                    ProductId = product.Id,
                    Quantity = 1
                }),
            "test-user");

        // Set up an order that is already in a terminal state.
        var order = await _context.Orders
            .FirstAsync(o => o.Id == created.Id);

        order.Status = terminalStatus;
        await _context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _service.UpdateStatusAsync(
                created.Id,
                new UpdateOrderStatusDto
                {
                    Status = OrderStatus.Confirmed
                }));

        Assert.Equal(409, exception.StatusCode);

        var persistedOrder = await _context.Orders
            .AsNoTracking()
            .FirstAsync(o => o.Id == created.Id);

        Assert.Equal(terminalStatus, persistedOrder.Status);
    }
}
