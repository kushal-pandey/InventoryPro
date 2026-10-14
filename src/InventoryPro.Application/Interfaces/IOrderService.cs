using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;

namespace InventoryPro.Application.Interfaces;

public interface IOrderService
{
    Task<PagedResult<OrderDto>> GetAllAsync(ListQueryParameters query);
    Task<OrderDto> GetByIdAsync(int id);
    Task<OrderDto> CreateAsync(CreateOrderDto dto, string userId);
    Task<OrderDto> UpdateStatusAsync(int id, UpdateOrderStatusDto dto);
}
