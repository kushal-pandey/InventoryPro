using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;

namespace InventoryPro.Application.Interfaces;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAllAsync(ProductQueryParameters queryParameters);
    Task<ProductDto> GetByIdAsync(int id);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task UpdateAsync(int id, UpdateProductDto dto);
    Task DeleteAsync(int id);
    Task<ProductDto> AdjustStockAsync(int id, AdjustStockDto dto);
    Task<IEnumerable<ProductDto>> GetLowStockAsync();
}
