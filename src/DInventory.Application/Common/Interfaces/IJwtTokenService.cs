using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user, string roleName);
    string GenerateRefreshTokenValue();
    string HashToken(string token);
}
