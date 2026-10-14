namespace InventoryPro.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int ReorderLevel { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Legacy schema column retained for migration compatibility; stock concurrency uses conditional database updates.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
