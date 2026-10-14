using InventoryPro.Application.Common;
using InventoryPro.Application.DTOs;
using InventoryPro.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService) => _customerService = customerService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerDto>>> GetAll([FromQuery] ListQueryParameters query)
        => Ok(await _customerService.GetAllAsync(query));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDto>> GetById(int id)
        => Ok(await _customerService.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerDto dto)
    {
        var created = await _customerService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
