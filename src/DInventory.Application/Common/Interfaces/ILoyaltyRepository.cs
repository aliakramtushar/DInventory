using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface ILoyaltyRepository
{
    Task<LoyaltySettings> GetSettingsAsync();
    Task<bool> UpdateSettingsAsync(LoyaltySettings settings);
    Task<int> GetBalanceAsync(int customerId);
    Task<IEnumerable<LoyaltyTransaction>> GetHistoryAsync(int customerId, int take = 100);
    Task<int> CreateTransactionAsync(LoyaltyTransaction transaction);
}
