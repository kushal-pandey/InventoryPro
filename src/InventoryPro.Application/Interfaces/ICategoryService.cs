using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;

namespace InventoryPro.Application.Interfaces;

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> GetAllAsync(ListQueryParameters query);
    Task<CategoryDto> GetByIdAsync(int id);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto);
    Task DeleteAsync(int id);
}
