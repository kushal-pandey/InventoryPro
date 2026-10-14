using AutoMapper;
using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;
using InventoryPro.Application.Interfaces;
using InventoryPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryPro.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public CategoryService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<PagedResult<CategoryDto>> GetAllAsync(ListQueryParameters query)
    {
        var source = _uow.Categories.Query().AsNoTracking().OrderBy(c => c.Name).ThenBy(c => c.Id);
        var totalCount = await source.CountAsync();
        var pageNumber = query.SafePageNumber;
        var pageSize = query.SafePageSize;
        var categories = await source.Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ProductCount = c.Products.Count(p => p.IsActive)
            })
            .ToListAsync();

        return new PagedResult<CategoryDto>
        {
            Items = categories,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CategoryDto> GetByIdAsync(int id)
    {
        var category = await _uow.Categories.Query().AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ProductCount = c.Products.Count(p => p.IsActive)
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException(nameof(Category), id);

        return category;
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        var nameTaken = await _uow.Categories.Query().AnyAsync(c => c.Name == dto.Name);
        if (nameTaken)
            throw new ConflictException($"A category named '{dto.Name}' already exists.");

        var category = _mapper.Map<Category>(dto);
        await _uow.Categories.AddAsync(category);
        await _uow.SaveChangesAsync();

        return _mapper.Map<CategoryDto>(category);
    }

    public async Task DeleteAsync(int id)
    {
        var category = await _uow.Categories.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Category), id);

        var hasProducts = await _uow.Products.Query().AnyAsync(p => p.CategoryId == id);
        if (hasProducts)
            throw new ConflictException("Cannot delete a category that still has products assigned to it.");

        _uow.Categories.Remove(category);
        await _uow.SaveChangesAsync();
    }
}
