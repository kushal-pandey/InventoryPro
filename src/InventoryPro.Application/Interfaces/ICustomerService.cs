using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;

namespace InventoryPro.Application.Interfaces;

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetAllAsync(ListQueryParameters query);
    Task<CustomerDto> GetByIdAsync(int id);
    Task<CustomerDto> CreateAsync(CreateCustomerDto dto);
}
