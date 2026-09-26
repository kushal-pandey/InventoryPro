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

    public async Task<IEnumerable<CustomerDto>> GetAllAsync()
    {
        var customers = await _uow.Customers.Query().ToListAsync();
        return _mapper.Map<IEnumerable<CustomerDto>>(customers);
    }

    public async Task<CustomerDto> GetByIdAsync(int id)
    {
        var customer = await _uow.Customers.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Customer), id);

        return _mapper.Map<CustomerDto>(customer);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto)
    {
        var emailTaken = await _uow.Customers.Query().AnyAsync(c => c.Email == dto.Email);
        if (emailTaken)
            throw new ConflictException($"A customer with email '{dto.Email}' already exists.");

        var customer = _mapper.Map<Customer>(dto);
        await _uow.Customers.AddAsync(customer);
        await _uow.SaveChangesAsync();

        return _mapper.Map<CustomerDto>(customer);
    }
}
