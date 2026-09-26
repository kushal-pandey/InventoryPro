using System.Security.Claims;
using InventoryPro.Application.DTOs;
using InventoryPro.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll()
        => Ok(await _orderService.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetById(int id)
        => Ok(await _orderService.GetByIdAsync(id));

    /// <summary>
    /// Places a new order. Stock is validated and decremented atomically for every
    /// line item — if any line fails (e.g. insufficient stock), the whole order is
    /// rolled back.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? "unknown";

        var created = await _orderService.CreateAsync(dto, userId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(int id, UpdateOrderStatusDto dto)
        => Ok(await _orderService.UpdateStatusAsync(id, dto));
}
