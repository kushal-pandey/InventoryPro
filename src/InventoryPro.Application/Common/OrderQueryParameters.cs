using System.ComponentModel.DataAnnotations;
using InventoryPro.Domain.Entities;

namespace InventoryPro.Application.Common;

public sealed class OrderQueryParameters : ListQueryParameters
{
    [MaxLength(100)]
    public string? Search { get; set; }

    [EnumDataType(typeof(OrderStatus))]
    public OrderStatus? Status { get; set; }

    [RegularExpression("^(orderDate|orderNumber|customer|total)$")]
    public string SortBy { get; set; } = "orderDate";

    public bool SortDescending { get; set; } = true;
}
