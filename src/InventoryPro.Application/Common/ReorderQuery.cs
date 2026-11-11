namespace InventoryPro.Application.Common;

/// <summary>Optional per-request overrides for the reorder planner (defaults come from configuration).</summary>
public class ReorderQuery
{
    public int? LookbackDays { get; set; }
    public int? LeadTimeDays { get; set; }
    public int? SafetyDays { get; set; }

    /// <summary>Maximum rows returned (1-200).</summary>
    public int Limit { get; set; } = 50;

    /// <summary>When false (default) products that are Healthy are left out.</summary>
    public bool IncludeHealthy { get; set; }
}
