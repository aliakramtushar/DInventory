using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IRefreshTokenRepository
{
    Task<int> CreateAsync(RefreshToken token);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);
    Task<bool> RevokeAsync(string tokenHash, string? revokedByIp, string? replacedByTokenHash = null);
    Task<int> RevokeAllForUserAsync(int userId, string? revokedByIp = null);
    Task<IEnumerable<RefreshToken>> GetActiveTokensForUserAsync(int userId);
}
