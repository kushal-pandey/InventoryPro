using InventoryPro.Application.Common;
using InventoryPro.Application.Insights;
using InventoryPro.Application.Services;
using InventoryPro.Domain.Entities;
using InventoryPro.Infrastructure.Data;
using InventoryPro.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InventoryPro.Tests;

public class InsightsServiceTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private InsightsService _service = null!;

    private int _fastId, _dustyId, _steadyId;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _context.Database.EnsureCreatedAsync();

        await SeedAsync();

        _service = new InsightsService(new UnitOfWork(_context), new InsightsSettings(), new FixedTimeProvider(Now));
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        var category = new Category { Name = "General" };
        var customer = new Customer { FullName = "Asha", Email = "asha@example.com" };
        _context.AddRange(category, customer);
        await _context.SaveChangesAsync();

        var old = Now.AddDays(-200);
        var fast = new Product { Name = "Fast", Sku = "F-1", Price = 100m, StockQuantity = 5, ReorderLevel = 5, CategoryId = category.Id, CreatedAt = old };
        var dusty = new Product { Name = "Dusty", Sku = "D-1", Price = 50m, StockQuantity = 20, ReorderLevel = 2, CategoryId = category.Id, CreatedAt = old };
        var steady = new Product { Name = "Steady", Sku = "S-1", Price = 10m, StockQuantity = 40, ReorderLevel = 5, CategoryId = category.Id, CreatedAt = old };
        var retired = new Product { Name = "Retired", Sku = "R-1", Price = 999m, StockQuantity = 99, ReorderLevel = 1, CategoryId = category.Id, CreatedAt = old, IsActive = false };
        _context.Products.AddRange(fast, dusty, steady, retired);
        await _context.SaveChangesAsync();

        _fastId = fast.Id;
        _dustyId = dusty.Id;
        _steadyId = steady.Id;

        AddOrder("ORD-1", customer.Id, Now.AddDays(-5), OrderStatus.Confirmed, 4000m, (fast.Id, 40, 100m));
        AddOrder("ORD-2", customer.Id, Now.AddDays(-10), OrderStatus.Pending, 2300m, (fast.Id, 20, 100m), (steady.Id, 30, 10m));
        // Cancelled sales must never count as demand or revenue.
        AddOrder("ORD-3", customer.Id, Now.AddDays(-3), OrderStatus.Cancelled, 50000m, (fast.Id, 500, 100m));
        // Outside every window.
        AddOrder("ORD-4", customer.Id, Now.AddDays(-90), OrderStatus.Delivered, 250m, (dusty.Id, 5, 50m));

        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private void AddOrder(string number, int customerId, DateTime date, OrderStatus status, decimal total,
        params (int ProductId, int Quantity, decimal UnitPrice)[] lines)
    {
        var order = new Order
        {
            OrderNumber = number, CustomerId = customerId, OrderDate = date, Status = status, TotalAmount = total
        };
        foreach (var line in lines)
            order.OrderItems.Add(new OrderItem { ProductId = line.ProductId, Quantity = line.Quantity, UnitPrice = line.UnitPrice });

        _context.Orders.Add(order);
    }

    [Fact]
    public async Task ReorderSuggestions_RankByUrgency_AndIgnoreCancelledOrders()
    {
        var suggestions = await _service.GetReorderSuggestionsAsync(new ReorderQuery());

        Assert.Equal(2, suggestions.Count); // Healthy "Steady" and inactive "Retired" are left out

        var fast = suggestions[0];
        Assert.Equal(_fastId, fast.ProductId);
        Assert.Equal("Critical", fast.Health);
        Assert.Equal(60, fast.UnitsSold);          // 40 + 20; the cancelled 500 is ignored
        Assert.Equal(2.0, fast.DailyDemand);
        Assert.Equal(2.5, fast.DaysOfCover);
        Assert.Equal(43, fast.SuggestedOrderQuantity);

        var dusty = suggestions[1];
        Assert.Equal(_dustyId, dusty.ProductId);
        Assert.Equal("Dead", dusty.Health);
        Assert.Equal(0, dusty.SuggestedOrderQuantity);
    }

    [Fact]
    public async Task ReorderSuggestions_CanIncludeHealthyProducts()
    {
        var suggestions = await _service.GetReorderSuggestionsAsync(new ReorderQuery { IncludeHealthy = true });

        Assert.Equal(new[] { _fastId, _steadyId, _dustyId }, suggestions.Select(s => s.ProductId).ToArray());
    }

    [Fact]
    public async Task ReorderSuggestions_RespectsLimit()
    {
        var suggestions = await _service.GetReorderSuggestionsAsync(new ReorderQuery { IncludeHealthy = true, Limit = 1 });

        Assert.Single(suggestions);
    }

    [Fact]
    public async Task LongerLeadTime_MakesTheSameStockMoreUrgent()
    {
        // 40 on hand at 1/day = 40 days of cover; a 45 day lead time means it runs out before delivery.
        var suggestions = await _service.GetReorderSuggestionsAsync(
            new ReorderQuery { LeadTimeDays = 45, SafetyDays = 0, IncludeHealthy = true });

        var steady = suggestions.Single(s => s.ProductId == _steadyId);
        Assert.Equal("Critical", steady.Health);
    }

    [Fact]
    public async Task Summary_ValuesStock_AndSeparatesSlowStock()
    {
        var summary = await _service.GetSummaryAsync();

        Assert.Equal(30, summary.WindowDays);
        Assert.Equal(3, summary.ActiveProducts);            // inactive product excluded
        Assert.Equal(65, summary.TotalUnitsOnHand);          // 5 + 20 + 40
        Assert.Equal(1900m, summary.InventoryValue);         // 500 + 1000 + 400
        Assert.Equal(1, summary.NeedsReorderCount);          // Fast
        Assert.Equal(0, summary.OutOfStockCount);
        Assert.Equal(1, summary.DeadStockCount);             // Dusty
        Assert.Equal(1000m, summary.SlowStockValue);         // Dusty: 20 x 50

        Assert.Equal(2, summary.OrdersInWindow);             // ORD-1, ORD-2 (cancelled and old ones excluded)
        Assert.Equal(6300m, summary.RevenueInWindow);
        Assert.Equal(3, summary.OrdersByStatus.Count);       // Pending, Confirmed, Cancelled inside the window
    }

    [Fact]
    public async Task SalesTrend_IsZeroFilled_AndExcludesCancelledOrders()
    {
        var trend = await _service.GetSalesTrendAsync(7);

        Assert.Equal(7, trend.Count);
        Assert.Equal(DateOnly.FromDateTime(Now.Date.AddDays(-6)), trend[0].Date);
        Assert.Equal(DateOnly.FromDateTime(Now.Date), trend[^1].Date);

        var busyDay = trend.Single(t => t.Date == DateOnly.FromDateTime(Now.AddDays(-5).Date));
        Assert.Equal(4000m, busyDay.Revenue);
        Assert.Equal(1, busyDay.Orders);

        Assert.Equal(4000m, trend.Sum(t => t.Revenue));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
