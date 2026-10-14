namespace InventoryPro.Application.Common;

public class ProductQueryParameters
{
    private const int MaxPageSize = 50;
    private int _pageSize = 10;
    private int _pageNumber = 1;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : Math.Min(value, int.MaxValue / MaxPageSize);
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            > MaxPageSize => MaxPageSize,
            < 1 => 1,
            _ => value
        };
    }

    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? LowStockOnly { get; set; }

    /// <summary>Allowed values: name, price, stock, createdAt</summary>
    public string? SortBy { get; set; } = "name";
    public bool SortDescending { get; set; }
}
