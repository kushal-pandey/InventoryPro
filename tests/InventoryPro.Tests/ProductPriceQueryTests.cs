using AutoMapper;
using InventoryPro.Application.Common;
using InventoryPro.Application.Mappings;
using InventoryPro.Application.Services;
using InventoryPro.Domain.Entities;
using InventoryPro.Infrastructure.Data;
using InventoryPro.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryPro.Tests;

/// <summary>
/// Regression tests for price sorting/filtering on SQLite (EF Core cannot translate ORDER BY or
/// comparisons on decimal columns there unless they are converted).
/// </summary>
public class ProductPriceQueryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private ProductService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _context.Database.EnsureCreatedAsync();

        var category = new Category { Name = "General" };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        _context.Products.AddRange(
            new Product { Name = "Mid", Sku = "P-2", Price = 20.50m, StockQuantity = 5, ReorderLevel = 1, CategoryId = category.Id },
            new Product { Name = "Low", Sku = "P-1", Price = 10m, StockQuantity = 5, ReorderLevel = 1, CategoryId = category.Id },
            new Product { Name = "High", Sku = "P-3", Price = 30.25m, StockQuantity = 5, ReorderLevel = 1, CategoryId = category.Id });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        _service = new ProductService(new UnitOfWork(_context), mapper);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task SortByPrice_Ascending_And_Descending()
    {
        var asc = await _service.GetAllAsync(new ProductQueryParameters { SortBy = "price" });
        var desc = await _service.GetAllAsync(new ProductQueryParameters { SortBy = "price", SortDescending = true });

        Assert.Equal(new[] { "Low", "Mid", "High" }, asc.Items.Select(p => p.Name).ToArray());
        Assert.Equal(new[] { "High", "Mid", "Low" }, desc.Items.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task PriceRangeFilter_KeepsDecimalPrecision()
    {
        var page = await _service.GetAllAsync(new ProductQueryParameters { MinPrice = 15m, MaxPrice = 25m });

        var product = Assert.Single(page.Items);
        Assert.Equal("Mid", product.Name);
        Assert.Equal(20.50m, product.Price);
    }
}
