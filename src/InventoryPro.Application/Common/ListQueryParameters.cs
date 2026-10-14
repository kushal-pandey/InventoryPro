namespace InventoryPro.Application.Common;

public class ListQueryParameters
{
    private int _pageNumber = 1;
    private int _pageSize = 20;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : Math.Min(value, int.MaxValue / 100);
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => value
        };
    }

    public int SafePageNumber => PageNumber;
    public int SafePageSize => PageSize;
}
