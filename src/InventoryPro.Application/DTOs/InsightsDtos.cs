namespace InventoryPro.Application.DTOs;

public class InventorySummaryDto
{
    /// <summary>Days of history used for the sales-based figures below.</summary>
    public int WindowDays { get; set; }

    public int ActiveProducts { get; set; }
    public int TotalUnitsOnHand { get; set; }

    /// <summary>Stock on hand valued at selling price (the data model has no cost price yet).</summary>
    public decimal InventoryValue { get; set; }

    public int OutOfStockCount { get; set; }

    /// <summary>Out of stock + critical + reorder soon.</summary>
    public int NeedsReorderCount { get; set; }

    public int OverstockedCount { get; set; }
    public int DeadStockCount { get; set; }

    /// <summary>Selling-price value of stock that is dead or overstocked, i.e. cash that is not turning over.</summary>
    public decimal SlowStockValue { get; set; }

    /// <summary>Orders placed in the window, excluding cancelled ones.</summary>
    public int OrdersInWindow { get; set; }
    public decimal RevenueInWindow { get; set; }

    public List<OrderStatusCountDto> OrdersByStatus { get; set; } = new();
}

public class OrderStatusCountDto
{
    /// <summary>Numeric OrderStatus value (same encoding as the orders API).</summary>
    public int Status { get; set; }
    public int Count { get; set; }
}

public class ReorderSuggestionDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public int StockOnHand { get; set; }
    public int ReorderLevel { get; set; }

    public int UnitsSold { get; set; }
    public int WindowDays { get; set; }
    public double DailyDemand { get; set; }

    /// <summary>Null when the product has not sold in the window (cover cannot be estimated).</summary>
    public double? DaysOfCover { get; set; }

    public int ReorderPoint { get; set; }
    public int SuggestedOrderQuantity { get; set; }

    /// <summary>OutOfStock, Critical, ReorderSoon, Healthy, Overstocked or Dead.</summary>
    public string Health { get; set; } = string.Empty;
}

public class SalesTrendPointDto
{
    public DateOnly Date { get; set; }
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
}
