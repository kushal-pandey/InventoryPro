using System.ComponentModel.DataAnnotations;

namespace InventoryPro.API.Models;

/// <summary>Fields required to create an Admin or Staff account.</summary>
public sealed record CreateUserRequest
{
    /// <summary>The name shown for the account.</summary>
    [Required, StringLength(150, MinimumLength = 1), RegularExpression(@".*\S.*")]
    public required string FullName { get; init; }

    /// <summary>The unique sign-in email address.</summary>
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    /// <summary>A password that satisfies the configured Identity password policy.</summary>
    [Required, StringLength(256, MinimumLength = 1)]
    public required string Password { get; init; }

    /// <summary>The account role, either Admin or Staff.</summary>
    [Required, RegularExpression("^(Admin|Staff)$")]
    public required string Role { get; init; }
}

/// <summary>The role to assign to an existing account.</summary>
public sealed record UpdateUserRoleRequest
{
    /// <summary>The account role, either Admin or Staff.</summary>
    [Required, RegularExpression("^(Admin|Staff)$")]
    public required string Role { get; init; }
}

/// <summary>The requested active state for an account.</summary>
public sealed record UpdateUserActiveRequest
{
    /// <summary>True to activate the account and clear any lockout; otherwise deactivate it.</summary>
    [Required]
    public required bool IsActive { get; init; }
}

/// <summary>Safe account details exposed to administrators.</summary>
public sealed record UserResponse(
    string Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool IsLockedOut,
    DateTimeOffset CreatedAt);
