
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

public sealed class ProductServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private ProductService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        var category = new Category
        {
            Name = "Electronics",
            Description = "Electronic products"
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var configuration = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance);

        var mapper = configuration.CreateMapper();
        var unitOfWork = new UnitOfWork(_context);

        _service = new ProductService(unitOfWork, mapper);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<Product> AddProductAsync(
        string name = "Keyboard",
        string sku = "KB-001",
        decimal price = 1500m,
        int stock = 20,
        int reorderLevel = 5,
        bool isActive = true)
    {
        var category = await _context.Categories.FirstAsync();

        var product = new Product
        {
            Name = name,
            Sku = sku,
            Price = price,
            StockQuantity = stock,
            ReorderLevel = reorderLevel,
            CategoryId = category.Id,
            Category = category,
            IsActive = isActive
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return product;
    }

    [Fact]
    public async Task CreateAsync_ValidProduct_CreatesProduct()
    {
        var category = await _context.Categories.FirstAsync();

        var dto = new CreateProductDto
        {
            Name = "Wireless Mouse",
            Description = "Bluetooth mouse",
            Sku = "MS-001",
            Price = 799m,
            StockQuantity = 25,
            ReorderLevel = 5,
            CategoryId = category.Id
        };

        var result = await _service.CreateAsync(dto);

        Assert.True(result.Id > 0);
        Assert.Equal("Wireless Mouse", result.Name);
        Assert.Equal("MS-001", result.Sku);
        Assert.Equal(799m, result.Price);
        Assert.Equal(25, result.StockQuantity);
        Assert.Equal("Electronics", result.CategoryName);
    }

    [Fact]
    public async Task CreateAsync_DuplicateSku_ThrowsConflictException()
    {
        await AddProductAsync(sku: "DUP-001");

        var category = await _context.Categories.FirstAsync();

        var dto = new CreateProductDto
        {
            Name = "Another Keyboard",
            Sku = "DUP-001",
            Price = 1000m,
            StockQuantity = 10,
            ReorderLevel = 2,
            CategoryId = category.Id
        };

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync(dto));

        Assert.Contains("DUP-001", exception.Message);
        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_MissingCategory_ThrowsNotFoundException()
    {
        var dto = new CreateProductDto
        {
            Name = "Monitor",
            Sku = "MON-001",
            Price = 12000m,
            StockQuantity = 10,
            ReorderLevel = 2,
            CategoryId = 9999
        };

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateAsync(dto));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_MissingProduct_ThrowsNotFoundException()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(9999));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task AdjustStockAsync_IncreaseStock_UpdatesQuantity()
    {
        var product = await AddProductAsync(stock: 20);

        var result = await _service.AdjustStockAsync(
            product.Id,
            new AdjustStockDto
            {
                QuantityChange = 10,
                Reason = "Stock replenishment"
            });

        Assert.Equal(30, result.StockQuantity);
    }

    [Fact]
    public async Task AdjustStockAsync_DecreaseStock_UpdatesQuantity()
    {
        var product = await AddProductAsync(stock: 20);

        var result = await _service.AdjustStockAsync(
            product.Id,
            new AdjustStockDto
            {
                QuantityChange = -7,
                Reason = "Damaged stock"
            });

        Assert.Equal(13, result.StockQuantity);
    }

    [Fact]
    public async Task AdjustStockAsync_NegativeResult_ThrowsConflictException()
    {
        var product = await AddProductAsync(stock: 5);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _service.AdjustStockAsync(
                product.Id,
                new AdjustStockDto
                {
                    QuantityChange = -6,
                    Reason = "Invalid stock removal"
                }));

        Assert.Equal(409, exception.StatusCode);

        var unchangedProduct = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == product.Id);

        Assert.Equal(5, unchangedProduct.StockQuantity);
    }

    [Fact]
    public async Task GetLowStockAsync_ReturnsOnlyActiveLowStockProducts()
    {
        await AddProductAsync(
            name: "Low Stock Keyboard",
            sku: "LOW-001",
            stock: 2,
            reorderLevel: 5);

        await AddProductAsync(
            name: "Healthy Mouse",
            sku: "OK-001",
            stock: 20,
            reorderLevel: 5);

        await AddProductAsync(
            name: "Inactive Monitor",
            sku: "INACTIVE-001",
            stock: 1,
            reorderLevel: 5,
            isActive: false);

        var result = (await _service.GetLowStockAsync()).ToList();

        var product = Assert.Single(result);
        Assert.Equal("LOW-001", product.Sku);
        Assert.True(product.LowStock);
    }


    [Fact]
    public async Task GetAllAsync_SearchAndPagination_ReturnsMatchingResults()
    {
        await AddProductAsync(
            name: "Gaming Keyboard",
            sku: "GAME-001",
            price: 3000m);

        await AddProductAsync(
            name: "Office Keyboard",
            sku: "OFFICE-001",
            price: 1000m);

        await AddProductAsync(
            name: "Gaming Mouse",
            sku: "GAME-002",
            price: 2000m);

        var result = await _service.GetAllAsync(
            new ProductQueryParameters
            {
                Search = "gaming",
                PageNumber = 1,
                PageSize = 1,
                SortBy = "name"
            });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(1, result.PageSize);
        Assert.Single(result.Items);
        Assert.Equal("GAME-001", result.Items.First().Sku);
    }

    [Fact]
    public async Task UpdateAsync_ValidProduct_UpdatesProduct()
    {
        var product = await AddProductAsync();

        var dto = new UpdateProductDto
        {
            Name = "Updated Keyboard",
            Description = "Mechanical keyboard",
            Price = 2499m,
            ReorderLevel = 8,
            CategoryId = product.CategoryId,
            IsActive = true
        };

        await _service.UpdateAsync(product.Id, dto);

        var updated = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == product.Id);

        Assert.Equal("Updated Keyboard", updated.Name);
        Assert.Equal("Mechanical keyboard", updated.Description);
        Assert.Equal(2499m, updated.Price);
        Assert.Equal(8, updated.ReorderLevel);
        Assert.True(updated.IsActive);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_MissingProduct_ThrowsNotFoundException()
    {
        var dto = new UpdateProductDto
        {
            Name = "Updated Keyboard",
            Price = 2000m,
            ReorderLevel = 5,
            CategoryId = 1,
            IsActive = true
        };

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateAsync(9999, dto));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_MissingCategory_ThrowsNotFoundException()
    {
        var product = await AddProductAsync();
        var originalName = product.Name;

        var dto = new UpdateProductDto
        {
            Name = "Should Not Be Saved",
            Price = 2000m,
            ReorderLevel = 5,
            CategoryId = 9999,
            IsActive = true
        };

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateAsync(product.Id, dto));

        Assert.Equal(404, exception.StatusCode);

        var unchanged = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == product.Id);

        Assert.Equal(originalName, unchanged.Name);
    }

    [Fact]
    public async Task DeleteAsync_ProductWithoutOrderHistory_DeletesProduct()
    {
        var product = await AddProductAsync();

        await _service.DeleteAsync(product.Id);

        var deleted = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_ProductWithOrderHistory_SoftDeletesProduct()
    {
        var product = await AddProductAsync();

        var customer = new Customer
        {
            FullName = "Test Customer",
            Email = "customer@example.com"
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        var order = new Order
        {
            OrderNumber = "TEST-ORDER-001",
            CustomerId = customer.Id,
            Status = OrderStatus.Confirmed,
            TotalAmount = product.Price,
            CreatedByUserId = "test-user"
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        _context.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = 1,
            UnitPrice = product.Price
        });

        await _context.SaveChangesAsync();

        await _service.DeleteAsync(product.Id);

        var retained = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        Assert.NotNull(retained);
        Assert.False(retained.IsActive);

        // Order history must still reference the product.
        Assert.True(await _context.OrderItems
            .AnyAsync(oi => oi.ProductId == product.Id));
    }

    [Fact]
    public async Task AdjustStockAsync_ZeroChange_KeepsQuantityUnchanged()
    {
        var product = await AddProductAsync(stock: 20);

        var result = await _service.AdjustStockAsync(
            product.Id,
            new AdjustStockDto
            {
                QuantityChange = 0,
                Reason = "Inventory verification"
            });

        Assert.Equal(20, result.StockQuantity);

        var persisted = await _context.Products
            .AsNoTracking()
            .FirstAsync(p => p.Id == product.Id);

        Assert.Equal(20, persisted.StockQuantity);
    }

    [Fact]
    public async Task GetAllAsync_CategoryAndLowStockFilters_ReturnOnlyMatchingProducts()
    {
        var category = await _context.Categories.FirstAsync();

        await AddProductAsync(
            name: "Low Stock Keyboard",
            sku: "FILTER-LOW-001",
            stock: 2,
            reorderLevel: 5);

        await AddProductAsync(
            name: "Healthy Keyboard",
            sku: "FILTER-OK-001",
            stock: 20,
            reorderLevel: 5);

        var otherCategory = new Category
        {
            Name = "Office Supplies",
            Description = "Office products"
        };

        _context.Categories.Add(otherCategory);
        await _context.SaveChangesAsync();

        _context.Products.Add(new Product
        {
            Name = "Low Stock Notebook",
            Sku = "FILTER-LOW-002",
            Price = 100m,
            StockQuantity = 1,
            ReorderLevel = 5,
            CategoryId = otherCategory.Id,
            Category = otherCategory,
            IsActive = true
        });

        await _context.SaveChangesAsync();

        var result = await _service.GetAllAsync(
            new ProductQueryParameters
            {
                CategoryId = category.Id,
                LowStockOnly = true,
                SortBy = "name"
            });

        var productResult = Assert.Single(result.Items);
        Assert.Equal("FILTER-LOW-001", productResult.Sku);
        Assert.Equal(1, result.TotalCount);
        Assert.True(productResult.LowStock);
        Assert.Equal(category.Id, productResult.CategoryId);
    }


}
