using InventoryPro.Application.DTOs;
using InventoryPro.Application.Interfaces;
using InventoryPro.API.Models;
using InventoryPro.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InventoryPro.API.Controllers;

/// <summary>Account registration and JWT login endpoints.</summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly bool _allowPublicRegistration;

    /// <summary>Creates the authentication controller.</summary>
    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _allowPublicRegistration = configuration.GetValue<bool>("Auth:AllowPublicRegistration");
    }

    /// <summary>Returns whether public Staff registration is enabled.</summary>
    [HttpGet("registration-status")]
    [ProducesResponseType(typeof(RegistrationStatusDto), StatusCodes.Status200OK)]
    public ActionResult<RegistrationStatusDto> GetRegistrationStatus() =>
        Ok(new RegistrationStatusDto(_allowPublicRegistration));

    /// <summary>
    /// Creates a Staff user. Staff can read customer and order data, so by default only an
    /// Admin may create accounts (set Auth:AllowPublicRegistration=true to open sign-up).
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        if (!_allowPublicRegistration && !User.IsInRole("Admin"))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiErrorResponse.Create(
                    StatusCodes.Status403Forbidden,
                    "Registration is restricted. Ask an administrator to create your account."));
        }

        var email = dto.Email.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
            return Conflict(ApiErrorResponse.Create(
                StatusCodes.Status409Conflict,
                "A user with this email already exists."));

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = dto.FullName.Trim()
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(ApiErrorResponse.Create(
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                result.Errors
                    .GroupBy(error => error.Code)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.Description).ToArray())));

        var roleResult = await _userManager.AddToRoleAsync(user, "Staff");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiErrorResponse.Create(
                    StatusCodes.Status500InternalServerError,
                    "Unable to assign the default role."));
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

    /// <summary>Authenticates a user and returns a JWT session.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status423Locked)]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (user is null || !user.IsActive)
            return Unauthorized(ApiErrorResponse.Create(
                StatusCodes.Status401Unauthorized,
                "Invalid email or password."));

        var signInResult = await _signInManager.CheckPasswordSignInAsync(
            user, dto.Password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
            return StatusCode(StatusCodes.Status423Locked,
                ApiErrorResponse.Create(
                    StatusCodes.Status423Locked,
                    "Account temporarily locked. Try again later."));

        if (!signInResult.Succeeded)
            return Unauthorized(ApiErrorResponse.Create(
                StatusCodes.Status401Unauthorized,
                "Invalid email or password."));

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
