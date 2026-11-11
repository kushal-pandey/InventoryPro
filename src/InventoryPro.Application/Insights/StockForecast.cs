namespace InventoryPro.Application.Insights;

/// <summary>Ordered by urgency: the planner lists lower values first.</summary>
public enum StockHealth
{
    /// <summary>Nothing on the shelf.</summary>
    OutOfStock = 0,

    /// <summary>Will sell out before a new order could arrive (days of cover &lt; lead time).</summary>
    Critical = 1,

    /// <summary>At or below the reorder point; place an order soon.</summary>
    ReorderSoon = 2,

    Healthy = 3,

    /// <summary>Far more stock than the current sales rate needs; cash is tied up.</summary>
    Overstocked = 4,

    /// <summary>In stock but has not sold at all for a long time.</summary>
    Dead = 5
}

public readonly record struct StockForecastInput(
    int StockOnHand,
    int ReorderLevel,
    int UnitsSoldInWindow,
    int WindowDays,
    int UnitsSoldInDeadWindow,
    int ProductAgeDays,
    int LeadTimeDays,
    int SafetyDays,
    int OrderCoverageDays,
    int DeadStockDays,
    int OverstockCoverDays);

public readonly record struct StockForecast(
    double DailyDemand,
    double? DaysOfCover,
    int ReorderPoint,
    int SuggestedOrderQuantity,
    StockHealth Health)
{
    public bool NeedsReorder => Health is StockHealth.OutOfStock or StockHealth.Critical or StockHealth.ReorderSoon;
}

/// <summary>
/// Pure, database-free demand forecasting: turns "how much did we sell recently" into
/// "how many days will the stock last, and what should we order". Kept free of EF so the
/// rules are trivially unit-testable.
/// </summary>
public static class StockForecastCalculator
{
    public static StockForecast Calculate(StockForecastInput input)
    {
        var windowDays = Math.Max(1, input.WindowDays);
        var stock = Math.Max(0, input.StockOnHand);
        var dailyDemand = Math.Max(0, input.UnitsSoldInWindow) / (double)windowDays;

        double? daysOfCover = dailyDemand > 0 ? stock / dailyDemand : null;

        var demandHorizonDays = input.LeadTimeDays + input.SafetyDays;

        // Reorder point = demand during (lead time + safety buffer). The owner's configured static
        // ReorderLevel stays as a floor so slow or brand-new items still get flagged.
        var reorderPoint = Math.Max(input.ReorderLevel, CeilingSafe(dailyDemand * demandHorizonDays));

        // Order-up-to level: enough for lead time + safety + the configured coverage period.
        // With no sales history, fall back to refilling to twice the static reorder level.
        var orderUpTo = Math.Max(
            CeilingSafe(dailyDemand * (demandHorizonDays + input.OrderCoverageDays)),
            input.ReorderLevel * 2);

        StockHealth health;
        if (stock == 0)
            health = StockHealth.OutOfStock;
        else if (input.UnitsSoldInDeadWindow == 0 && input.ProductAgeDays >= input.DeadStockDays)
            health = StockHealth.Dead;
        else if (daysOfCover is not null && daysOfCover < input.LeadTimeDays)
            health = StockHealth.Critical;
        else if (stock <= reorderPoint)
            health = StockHealth.ReorderSoon;
        else if (daysOfCover is not null && daysOfCover > input.OverstockCoverDays)
            health = StockHealth.Overstocked;
        else
            health = StockHealth.Healthy;

        var needsReorder = health is StockHealth.OutOfStock or StockHealth.Critical or StockHealth.ReorderSoon;
        var suggestedQuantity = needsReorder ? Math.Max(0, orderUpTo - stock) : 0;

        return new StockForecast(dailyDemand, daysOfCover, reorderPoint, suggestedQuantity, health);
    }

    // Guards against floating-point noise such as 20.000000000000004 rounding up to 21.
    private static int CeilingSafe(double value) => (int)Math.Ceiling(Math.Round(value, 6));
}
