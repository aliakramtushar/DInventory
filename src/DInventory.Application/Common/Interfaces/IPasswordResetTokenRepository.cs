using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task<int> CreateAsync(PasswordResetToken token);
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash);
    Task<bool> MarkUsedAsync(int resetTokenId);
    Task InvalidateAllForUserAsync(int userId);
}
