using InventoryPro.Application.Insights;

namespace InventoryPro.Tests;

public class StockForecastCalculatorTests
{
    // Defaults: 30 days of sales, lead time 7, safety 3, cover 14 => reorder point = 10 days of demand.
    private static StockForecastInput Input(
        int stock = 20, int reorderLevel = 5, int sold = 30, int window = 30, int soldInDeadWindow = 30,
        int ageDays = 200, int lead = 7, int safety = 3, int coverage = 14, int deadDays = 60, int overstockDays = 90) =>
        new(stock, reorderLevel, sold, window, soldInDeadWindow, ageDays, lead, safety, coverage, deadDays, overstockDays);

    [Fact]
    public void FastMoverAboutToRunOut_IsCritical_AndSuggestsRefill()
    {
        // 60 sold in 30 days = 2/day; 5 on hand = 2.5 days of cover, shorter than the 7 day lead time.
        var result = StockForecastCalculator.Calculate(Input(stock: 5, sold: 60, soldInDeadWindow: 60));

        Assert.Equal(StockHealth.Critical, result.Health);
        Assert.Equal(2.0, result.DailyDemand);
        Assert.Equal(2.5, result.DaysOfCover);
        Assert.Equal(20, result.ReorderPoint);          // 2/day x (7 + 3) days
        Assert.Equal(43, result.SuggestedOrderQuantity); // order up to 2 x (7+3+14) = 48, minus 5 on hand
        Assert.True(result.NeedsReorder);
    }

    [Fact]
    public void NothingOnTheShelf_IsOutOfStock()
    {
        var result = StockForecastCalculator.Calculate(Input(stock: 0, sold: 30));

        Assert.Equal(StockHealth.OutOfStock, result.Health);
        Assert.Equal(24, result.SuggestedOrderQuantity); // 1/day x 24 days
    }

    [Fact]
    public void AtOrBelowReorderPoint_ButNotRunningOutBeforeDelivery_IsReorderSoon()
    {
        // 1/day, 9 on hand = 9 days of cover (>= 7 day lead time) but reorder point is 10.
        var result = StockForecastCalculator.Calculate(Input(stock: 9, sold: 30));

        Assert.Equal(StockHealth.ReorderSoon, result.Health);
        Assert.Equal(15, result.SuggestedOrderQuantity); // 24 - 9
    }

    [Fact]
    public void ComfortableStock_IsHealthy_AndSuggestsNothing()
    {
        var result = StockForecastCalculator.Calculate(Input(stock: 40, sold: 30));

        Assert.Equal(StockHealth.Healthy, result.Health);
        Assert.Equal(0, result.SuggestedOrderQuantity);
        Assert.False(result.NeedsReorder);
    }

    [Fact]
    public void FarMoreStockThanSalesNeed_IsOverstocked()
    {
        var result = StockForecastCalculator.Calculate(Input(stock: 500, sold: 30)); // 500 days of cover

        Assert.Equal(StockHealth.Overstocked, result.Health);
        Assert.Equal(0, result.SuggestedOrderQuantity);
    }

    [Fact]
    public void OldProductThatNeverSells_IsDead_EvenIfBelowStaticReorderLevel()
    {
        var result = StockForecastCalculator.Calculate(
            Input(stock: 3, reorderLevel: 5, sold: 0, soldInDeadWindow: 0, ageDays: 100));

        Assert.Equal(StockHealth.Dead, result.Health);
        Assert.Equal(0, result.SuggestedOrderQuantity); // never reorder something that does not sell
    }

    [Fact]
    public void BrandNewProductWithNoSalesYet_IsNotDead()
    {
        var result = StockForecastCalculator.Calculate(
            Input(stock: 20, sold: 0, soldInDeadWindow: 0, ageDays: 10));

        Assert.Equal(StockHealth.Healthy, result.Health);
        Assert.Null(result.DaysOfCover);
    }

    [Fact]
    public void StaticReorderLevel_StillFlagsItemsThatHaveNoSalesHistory()
    {
        var result = StockForecastCalculator.Calculate(
            Input(stock: 3, reorderLevel: 5, sold: 0, soldInDeadWindow: 0, ageDays: 10));

        Assert.Equal(StockHealth.ReorderSoon, result.Health);
        Assert.Equal(7, result.SuggestedOrderQuantity); // refill to 2 x reorder level (10) minus 3
    }

    [Fact]
    public void FloatingPointNoise_DoesNotInflateTheReorderPoint()
    {
        // 29 units in 7 days over a 7-day horizon is 29.000000000000004 in doubles; must still round to 29.
        var result = StockForecastCalculator.Calculate(
            Input(stock: 100, reorderLevel: 0, sold: 29, window: 7, soldInDeadWindow: 29, lead: 4, safety: 3));

        Assert.Equal(29, result.ReorderPoint);
    }

    [Fact]
    public void ZeroLengthWindow_DoesNotDivideByZero()
    {
        var result = StockForecastCalculator.Calculate(Input(sold: 10, window: 0));

        Assert.Equal(10.0, result.DailyDemand);
    }
}
