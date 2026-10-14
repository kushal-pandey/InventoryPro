using InventoryPro.Application.DTOs;
using InventoryPro.Application.Interfaces;
using InventoryPro.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InventoryPro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    /// <summary>Registers a new user with the least-privileged Staff role.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        var email = dto.Email.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
            return Conflict(new { message = "A user with this email already exists." });

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = dto.FullName.Trim()
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        var roleResult = await _userManager.AddToRoleAsync(user, "Staff");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Unable to assign the default role." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);

        return Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            Email = user.Email!,
            FullName = user.FullName,
            Roles = roles
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        var signInResult = await _signInManager.CheckPasswordSignInAsync(
            user, dto.Password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
            return StatusCode(StatusCodes.Status423Locked,
                new { message = "Account temporarily locked. Try again later." });

        if (!signInResult.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);

        return Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            Email = user.Email!,
            FullName = user.FullName,
            Roles = roles
        });
    }
}
