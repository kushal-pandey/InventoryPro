using InventoryPro.Application.DTOs;

namespace InventoryPro.Application.Interfaces;

public interface IOrderService
{
    Task<IEnumerable<OrderDto>> GetAllAsync();
    Task<OrderDto> GetByIdAsync(int id);
    Task<OrderDto> CreateAsync(CreateOrderDto dto, string userId);
    Task<OrderDto> UpdateStatusAsync(int id, UpdateOrderStatusDto dto);
}
