namespace InventoryPro.Application.Insights;

/// <summary>
/// Tunable assumptions behind the reorder planner. Bound from the "Insights" configuration
/// section; callers can override lookback / lead time / safety days per request.
/// </summary>
public class InsightsSettings
{
    /// <summary>How many recent days of sales are used to estimate daily demand.</summary>
    public int LookbackDays { get; set; } = 30;

    /// <summary>Typical days between placing a supplier order and receiving stock.</summary>
    public int LeadTimeDays { get; set; } = 7;

    /// <summary>Extra days of demand kept as a buffer against delays and demand spikes.</summary>
    public int SafetyDays { get; set; } = 3;

    /// <summary>When reordering, buy enough to also cover this many days beyond lead time + safety.</summary>
    public int OrderCoverageDays { get; set; } = 14;

    /// <summary>A product with stock, older than this, and no sales in this window is "dead stock".</summary>
    public int DeadStockDays { get; set; } = 60;

    /// <summary>More than this many days of cover at the current sales rate is "overstocked".</summary>
    public int OverstockCoverDays { get; set; } = 90;

    public void Validate()
    {
        Require(LookbackDays, 7, 180, nameof(LookbackDays));
        Require(LeadTimeDays, 0, 60, nameof(LeadTimeDays));
        Require(SafetyDays, 0, 60, nameof(SafetyDays));
        Require(OrderCoverageDays, 0, 180, nameof(OrderCoverageDays));
        Require(DeadStockDays, 14, 365, nameof(DeadStockDays));
        Require(OverstockCoverDays, 30, 730, nameof(OverstockCoverDays));

        if (DeadStockDays < LookbackDays)
            throw new InvalidOperationException("Insights:DeadStockDays must be at least Insights:LookbackDays.");
    }

    private static void Require(int value, int min, int max, string name)
    {
        if (value < min || value > max)
            throw new InvalidOperationException($"Insights:{name} must be between {min} and {max} (was {value}).");
    }
}
