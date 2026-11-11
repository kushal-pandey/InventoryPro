using System.Security.Claims;
using InventoryPro.API.Models;
using InventoryPro.Application.Common;
using InventoryPro.Domain.Entities;
using InventoryPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryPro.API.Controllers;

/// <summary>Administrative account listing and management.</summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
public sealed class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    /// <summary>Creates the user-management controller.</summary>
    public UsersController(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    /// <summary>Returns a paged user directory including each user's roles and account state.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<UserResponse>>> GetAll(
        [FromQuery] ListQueryParameters query,
        CancellationToken cancellationToken)
    {
        var usersQuery = _userManager.Users.AsNoTracking();
        var totalCount = await usersQuery.CountAsync(cancellationToken);
        var pageNumber = query.SafePageNumber;
        var pageSize = query.SafePageSize;
        var users = await usersQuery
            .OrderBy(user => user.NormalizedEmail)
            .ThenBy(user => user.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var userIds = users.Select(user => user.Id).ToArray();
        var memberships = await _db.UserRoles
            .Where(membership => userIds.Contains(membership.UserId))
            .Join(
                _db.Roles,
                membership => membership.RoleId,
                role => role.Id,
                (membership, role) => new { membership.UserId, role.Name })
            .ToListAsync(cancellationToken);
        var rolesByUser = memberships
            .GroupBy(membership => membership.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(membership => membership.Name!)
                    .OrderBy(role => role, StringComparer.Ordinal)
                    .ToArray());
        var now = DateTimeOffset.UtcNow;
        var items = users
            .Select(user => ToResponse(
                user,
                rolesByUser.TryGetValue(user.Id, out var roles) ? roles : Array.Empty<string>(),
                now))
            .ToList();

        return Ok(new PagedResult<UserResponse>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    /// <summary>Returns one account by its Identity identifier.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound(ApiErrorResponse.Create(StatusCodes.Status404NotFound, "User not found."));

        return Ok(await ToResponseAsync(user));
    }

    /// <summary>Creates a user and assigns the requested Admin or Staff role.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request)
    {
        var email = request.Email.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
            return Conflict(ApiErrorResponse.Create(
                StatusCodes.Status409Conflict,
                "A user with this email already exists."));

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            IsActive = true
        };
        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var statusCode = createResult.Errors.Any(error =>
                error.Code is "DuplicateEmail" or "DuplicateUserName")
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, IdentityErrorResponse(statusCode, createResult));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiErrorResponse.Create(
                        StatusCodes.Status500InternalServerError,
                        "The account role could not be assigned and account cleanup failed."));

            return StatusCode(StatusCodes.Status500InternalServerError,
                IdentityErrorResponse(StatusCodes.Status500InternalServerError, roleResult));
        }

        var response = await ToResponseAsync(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, response);
    }

    /// <summary>Changes another user's role and invalidates their existing JWT sessions.</summary>
    [HttpPatch("{id}/role")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> UpdateRole(string id, UpdateUserRoleRequest request)
    {
        if (string.Equals(GetCurrentUserId(), id, StringComparison.Ordinal))
            return Conflict(ApiErrorResponse.Create(
                StatusCodes.Status409Conflict,
                "You cannot change your own role."));

        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound(ApiErrorResponse.Create(StatusCodes.Status404NotFound, "User not found."));

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count == 1 &&
            string.Equals(currentRoles[0], request.Role, StringComparison.Ordinal))
            return Ok(await ToResponseAsync(user, currentRoles));

        var addedRole = !currentRoles.Contains(request.Role, StringComparer.Ordinal);
        if (addedRole)
        {
            var addResult = await _userManager.AddToRoleAsync(user, request.Role);
            if (!addResult.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError,
                    IdentityErrorResponse(StatusCodes.Status500InternalServerError, addResult));
        }

        var rolesToRemove = currentRoles
            .Where(role => !string.Equals(role, request.Role, StringComparison.Ordinal))
            .ToArray();
        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                var rollbackSucceeded = !addedRole ||
                    (await _userManager.RemoveFromRoleAsync(user, request.Role)).Succeeded;
                var message = rollbackSucceeded
                    ? "The previous role could not be removed."
                    : "The previous role could not be removed and the new role could not be rolled back.";
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiErrorResponse.Create(StatusCodes.Status500InternalServerError, message));
            }
        }

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
            return StatusCode(StatusCodes.Status500InternalServerError,
                IdentityErrorResponse(StatusCodes.Status500InternalServerError, stampResult));

        return Ok(await ToResponseAsync(user));
    }

    /// <summary>Activates or deactivates another account; activation also clears lockout state.</summary>
    [HttpPatch("{id}/active")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> UpdateActive(string id, UpdateUserActiveRequest request)
    {
        if (!request.IsActive && string.Equals(GetCurrentUserId(), id, StringComparison.Ordinal))
            return Conflict(ApiErrorResponse.Create(
                StatusCodes.Status409Conflict,
                "You cannot deactivate your own account."));

        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound(ApiErrorResponse.Create(StatusCodes.Status404NotFound, "User not found."));

        var now = DateTimeOffset.UtcNow;
        var hasLockout = user.LockoutEnd.HasValue && user.LockoutEnd.Value > now;
        var shouldInvalidateSessions =
            user.IsActive != request.IsActive ||
            (request.IsActive && (hasLockout || user.AccessFailedCount > 0));

        user.IsActive = request.IsActive;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return StatusCode(StatusCodes.Status500InternalServerError,
                IdentityErrorResponse(StatusCodes.Status500InternalServerError, updateResult));

        if (request.IsActive)
        {
            var resetResult = await _userManager.ResetAccessFailedCountAsync(user);
            var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
            if (!resetResult.Succeeded || !unlockResult.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiErrorResponse.Create(
                        StatusCodes.Status500InternalServerError,
                        "The account was updated but its lockout could not be cleared."));
        }

        if (shouldInvalidateSessions)
        {
            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError,
                    IdentityErrorResponse(StatusCodes.Status500InternalServerError, stampResult));
        }

        return Ok(await ToResponseAsync(user));
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

    private async Task<UserResponse> ToResponseAsync(ApplicationUser user, IList<string>? roles = null)
    {
        roles ??= await _userManager.GetRolesAsync(user);
        return ToResponse(user, roles.ToArray(), DateTimeOffset.UtcNow);
    }

    private static UserResponse ToResponse(
        ApplicationUser user,
        IReadOnlyList<string> roles,
        DateTimeOffset now) =>
        new(
            user.Id,
            user.FullName,
            user.Email ?? string.Empty,
            roles,
            user.IsActive,
            user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd.Value > now,
            new DateTimeOffset(DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc)));

    private static ApiErrorResponse IdentityErrorResponse(int statusCode, IdentityResult result) =>
        ApiErrorResponse.Create(
            statusCode,
            statusCode == StatusCodes.Status400BadRequest
                ? "One or more validation errors occurred."
                : "The account operation could not be completed.",
            result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray()));
}
