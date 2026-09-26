using InventoryPro.Domain.Entities;

namespace InventoryPro.Application.Interfaces;

public interface ITokenService
{
    (string token, DateTime expiresAt) GenerateToken(ApplicationUser user, IList<string> roles);
}
