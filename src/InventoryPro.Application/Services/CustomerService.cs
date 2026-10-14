using AutoMapper;
using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;
using InventoryPro.Application.Interfaces;
using InventoryPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryPro.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public CustomerService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<PagedResult<CustomerDto>> GetAllAsync(ListQueryParameters query)
    {
        var source = _uow.Customers.Query().AsNoTracking()
            .OrderBy(c => c.FullName).ThenBy(c => c.Id);
        var totalCount = await source.CountAsync();
        var pageNumber = query.SafePageNumber;
        var pageSize = query.SafePageSize;
        var customers = await source.Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<CustomerDto>
        {
            Items = _mapper.Map<List<CustomerDto>>(customers),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CustomerDto> GetByIdAsync(int id)
    {
        var customer = await _uow.Customers.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Customer), id);

        return _mapper.Map<CustomerDto>(customer);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto)
    {
        var email = dto.Email.Trim();
        var emailTaken = await _uow.Customers.Query()
            .AnyAsync(c => c.Email.ToLower() == email.ToLower());
        if (emailTaken)
            throw new ConflictException($"A customer with email '{email}' already exists.");

        dto.Email = email;
        var customer = _mapper.Map<Customer>(dto);
        await _uow.Customers.AddAsync(customer);
        await _uow.SaveChangesAsync();

        return _mapper.Map<CustomerDto>(customer);
    }
}
