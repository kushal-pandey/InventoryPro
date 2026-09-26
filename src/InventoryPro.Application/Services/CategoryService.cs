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

    public async Task<IEnumerable<CategoryDto>> GetAllAsync()
    {
        var categories = await _uow.Categories.Query().Include(c => c.Products).ToListAsync();
        return _mapper.Map<IEnumerable<CategoryDto>>(categories);
    }

    public async Task<CategoryDto> GetByIdAsync(int id)
    {
        var category = await _uow.Categories.Query().Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException(nameof(Category), id);

        return _mapper.Map<CategoryDto>(category);
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
