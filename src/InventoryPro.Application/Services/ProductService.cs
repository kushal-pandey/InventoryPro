using AutoMapper;
using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;
using InventoryPro.Application.Interfaces;
using InventoryPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using AutoMapper.QueryableExtensions;

namespace InventoryPro.Application.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ProductService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<PagedResult<ProductDto>> GetAllAsync(ProductQueryParameters q)
    {
        var query = _uow.Products.Query().Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term));
        }

        if (q.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == q.CategoryId.Value);

        if (q.MinPrice.HasValue)
            query = query.Where(p => p.Price >= q.MinPrice.Value);

        if (q.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= q.MaxPrice.Value);

        if (q.LowStockOnly == true)
            query = query.Where(p => p.StockQuantity <= p.ReorderLevel);

        query = (q.SortBy?.ToLower(), q.SortDescending) switch
        {
            ("price", false) => query.OrderBy(p => p.Price),
            ("price", true) => query.OrderByDescending(p => p.Price),
            ("stock", false) => query.OrderBy(p => p.StockQuantity),
            ("stock", true) => query.OrderByDescending(p => p.StockQuantity),
            ("createdat", false) => query.OrderBy(p => p.CreatedAt),
            ("createdat", true) => query.OrderByDescending(p => p.CreatedAt),
            (_, true) => query.OrderByDescending(p => p.Name),
            _ => query.OrderBy(p => p.Name)
        };

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .ProjectTo<ProductDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        return new PagedResult<ProductDto>
        {
            Items = items,
            PageNumber = q.PageNumber,
            PageSize = q.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ProductDto> GetByIdAsync(int id)
    {
        var product = await _uow.Products.Query().Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException(nameof(Product), id);

        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        var categoryExists = await _uow.Categories.Query().AnyAsync(c => c.Id == dto.CategoryId);
        if (!categoryExists)
            throw new NotFoundException(nameof(Category), dto.CategoryId);

        var skuTaken = await _uow.Products.Query().AnyAsync(p => p.Sku == dto.Sku);
        if (skuTaken)
            throw new ConflictException($"A product with SKU '{dto.Sku}' already exists.");

        var product = _mapper.Map<Product>(dto);
        await _uow.Products.AddAsync(product);
        await _uow.SaveChangesAsync();

        return await GetByIdAsync(product.Id);
    }

    public async Task UpdateAsync(int id, UpdateProductDto dto)
    {
        var product = await _uow.Products.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Product), id);

        var categoryExists = await _uow.Categories.Query().AnyAsync(c => c.Id == dto.CategoryId);
        if (!categoryExists)
            throw new NotFoundException(nameof(Category), dto.CategoryId);

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.ReorderLevel = dto.ReorderLevel;
        product.CategoryId = dto.CategoryId;
        product.IsActive = dto.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var product = await _uow.Products.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Product), id);

        var hasOrders = await _uow.OrderItems.Query().AnyAsync(oi => oi.ProductId == id);
        if (hasOrders)
        {
            // Soft-delete: keep history intact for existing orders.
            product.IsActive = false;
            _uow.Products.Update(product);
        }
        else
        {
            _uow.Products.Remove(product);
        }

        await _uow.SaveChangesAsync();
    }

    public async Task<ProductDto> AdjustStockAsync(int id, AdjustStockDto dto)
    {
        var product = await _uow.Products.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Product), id);

        var newQuantity = product.StockQuantity + dto.QuantityChange;
        if (newQuantity < 0)
            throw new ConflictException($"Stock adjustment would result in negative stock ({newQuantity}).");

        product.StockQuantity = newQuantity;
        product.UpdatedAt = DateTime.UtcNow;

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<IEnumerable<ProductDto>> GetLowStockAsync()
    {
        var products = await _uow.Products.Query().Include(p => p.Category)
            .Where(p => p.StockQuantity <= p.ReorderLevel && p.IsActive)
            .ToListAsync();

        return _mapper.Map<IEnumerable<ProductDto>>(products);
    }
}
