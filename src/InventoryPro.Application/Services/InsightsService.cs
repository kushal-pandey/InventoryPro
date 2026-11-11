using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;
using InventoryPro.Application.Insights;
using InventoryPro.Application.Interfaces;
using InventoryPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryPro.Application.Services;

/// <summary>
/// Read-only analytics over products and orders. Aggregation that SQLite cannot translate
/// (decimal sums, date bucketing) is done in memory on a bounded result set, which is
/// appropriate for the small-business scale this app targets.
/// </summary>
public class InsightsService : IInsightsService
{
    private readonly IUnitOfWork _uow;
    private readonly InsightsSettings _settings;
    private readonly TimeProvider _time;

    public InsightsService(IUnitOfWork uow, InsightsSettings settings, TimeProvider time)
    {
        _uow = uow;
        _settings = settings;
        _time = time;
    }

    public async Task<InventorySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var windowStart = now.AddDays(-_settings.LookbackDays);

        var products = await LoadActiveProductsAsync(cancellationToken);
        var soldInWindow = await UnitsSoldSinceAsync(windowStart, cancellationToken);
        var soldInDeadWindow = await UnitsSoldSinceAsync(now.AddDays(-_settings.DeadStockDays), cancellationToken);

        var summary = new InventorySummaryDto
        {
            WindowDays = _settings.LookbackDays,
            ActiveProducts = products.Count
        };

        foreach (var product in products)
        {
            var forecast = Forecast(product, soldInWindow, soldInDeadWindow, now,
                _settings.LookbackDays, _settings.LeadTimeDays, _settings.SafetyDays);

            var value = product.StockQuantity * product.Price;
            summary.TotalUnitsOnHand += product.StockQuantity;
            summary.InventoryValue += value;

            if (forecast.NeedsReorder) summary.NeedsReorderCount++;

            switch (forecast.Health)
            {
                case StockHealth.OutOfStock:
                    summary.OutOfStockCount++;
                    break;
                case StockHealth.Overstocked:
                    summary.OverstockedCount++;
                    summary.SlowStockValue += value;
                    break;
                case StockHealth.Dead:
                    summary.DeadStockCount++;
                    summary.SlowStockValue += value;
                    break;
            }
        }

        var orders = await _uow.Orders.Query()
            .Where(o => o.OrderDate >= windowStart)
            .Select(o => new { o.Status, o.TotalAmount })
            .ToListAsync(cancellationToken);

        summary.OrdersByStatus = orders
            .GroupBy(o => o.Status)
            .OrderBy(g => g.Key)
            .Select(g => new OrderStatusCountDto { Status = (int)g.Key, Count = g.Count() })
            .ToList();

        var active = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        summary.OrdersInWindow = active.Count;
        summary.RevenueInWindow = active.Sum(o => o.TotalAmount);

        return summary;
    }

    public async Task<IReadOnlyList<ReorderSuggestionDto>> GetReorderSuggestionsAsync(
        ReorderQuery query, CancellationToken cancellationToken = default)
    {
        var lookback = query.LookbackDays ?? _settings.LookbackDays;
        var leadTime = query.LeadTimeDays ?? _settings.LeadTimeDays;
        var safety = query.SafetyDays ?? _settings.SafetyDays;
        var limit = Math.Clamp(query.Limit, 1, 200);

        var now = _time.GetUtcNow().UtcDateTime;
        var products = await LoadActiveProductsAsync(cancellationToken);
        var soldInWindow = await UnitsSoldSinceAsync(now.AddDays(-lookback), cancellationToken);
        var soldInDeadWindow = await UnitsSoldSinceAsync(
            now.AddDays(-Math.Max(_settings.DeadStockDays, lookback)), cancellationToken);

        var rows = new List<(ReorderSuggestionDto Dto, StockHealth Health, double? Cover)>(products.Count);

        foreach (var product in products)
        {
            var forecast = Forecast(product, soldInWindow, soldInDeadWindow, now, lookback, leadTime, safety);

            if (!query.IncludeHealthy && forecast.Health == StockHealth.Healthy)
                continue;

            soldInWindow.TryGetValue(product.Id, out var unitsSold);

            rows.Add((new ReorderSuggestionDto
            {
                ProductId = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                CategoryName = product.CategoryName,
                Price = product.Price,
                StockOnHand = product.StockQuantity,
                ReorderLevel = product.ReorderLevel,
                UnitsSold = unitsSold,
                WindowDays = lookback,
                DailyDemand = Math.Round(forecast.DailyDemand, 2),
                DaysOfCover = forecast.DaysOfCover is null ? null : Math.Round(forecast.DaysOfCover.Value, 1),
                ReorderPoint = forecast.ReorderPoint,
                SuggestedOrderQuantity = forecast.SuggestedOrderQuantity,
                Health = forecast.Health.ToString()
            }, forecast.Health, forecast.DaysOfCover));
        }

        return rows
            .OrderBy(r => r.Health)
            .ThenBy(r => r.Cover ?? double.MaxValue)
            .ThenBy(r => r.Dto.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(r => r.Dto)
            .ToList();
    }

    public async Task<IReadOnlyList<SalesTrendPointDto>> GetSalesTrendAsync(
        int days, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 365);

        var today = _time.GetUtcNow().UtcDateTime.Date;
        var start = today.AddDays(-(days - 1));

        var orders = await _uow.Orders.Query()
            .Where(o => o.OrderDate >= start && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.OrderDate, o.TotalAmount })
            .ToListAsync(cancellationToken);

        var byDay = orders
            .GroupBy(o => o.OrderDate.Date)
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(o => o.TotalAmount), Count: g.Count()));

        var points = new List<SalesTrendPointDto>(days);
        for (var i = 0; i < days; i++)
        {
            var day = start.AddDays(i);
            byDay.TryGetValue(day, out var bucket);

            points.Add(new SalesTrendPointDto
            {
                Date = DateOnly.FromDateTime(day),
                Revenue = bucket.Revenue,
                Orders = bucket.Count
            });
        }

        return points;
    }

    private StockForecast Forecast(
        StockProductRow product,
        IReadOnlyDictionary<int, int> soldInWindow,
        IReadOnlyDictionary<int, int> soldInDeadWindow,
        DateTime now,
        int windowDays,
        int leadTimeDays,
        int safetyDays)
    {
        soldInWindow.TryGetValue(product.Id, out var window);
        soldInDeadWindow.TryGetValue(product.Id, out var deadWindow);

        return StockForecastCalculator.Calculate(new StockForecastInput(
            StockOnHand: product.StockQuantity,
            ReorderLevel: product.ReorderLevel,
            UnitsSoldInWindow: window,
            WindowDays: windowDays,
            UnitsSoldInDeadWindow: deadWindow,
            ProductAgeDays: (int)Math.Max(0, (now - product.CreatedAt).TotalDays),
            LeadTimeDays: leadTimeDays,
            SafetyDays: safetyDays,
            OrderCoverageDays: _settings.OrderCoverageDays,
            DeadStockDays: _settings.DeadStockDays,
            OverstockCoverDays: _settings.OverstockCoverDays));
    }

    private async Task<List<StockProductRow>> LoadActiveProductsAsync(CancellationToken cancellationToken) =>
        await _uow.Products.Query()
            .Where(p => p.IsActive)
            .Select(p => new StockProductRow(
                p.Id, p.Name, p.Sku, p.Category.Name, p.StockQuantity, p.ReorderLevel, p.Price, p.CreatedAt))
            .ToListAsync(cancellationToken);

    /// <summary>Units sold per product since a date, ignoring cancelled orders.</summary>
    private async Task<Dictionary<int, int>> UnitsSoldSinceAsync(DateTime since, CancellationToken cancellationToken) =>
        await _uow.OrderItems.Query()
            .Where(oi => oi.Order.OrderDate >= since && oi.Order.Status != OrderStatus.Cancelled)
            .GroupBy(oi => oi.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(oi => oi.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Units, cancellationToken);

}

internal sealed record StockProductRow(
    int Id,
    string Name,
    string Sku,
    string CategoryName,
    int StockQuantity,
    int ReorderLevel,
    decimal Price,
    DateTime CreatedAt);
