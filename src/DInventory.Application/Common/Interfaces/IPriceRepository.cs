using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IPriceRepository
{
    Task<Price?> GetActivePriceAsync(int productId);
    Task<IEnumerable<Price>> GetHistoryAsync(int productId);
    Task<int> CreateAsync(Price price);
    Task DeactivateAllForProductAsync(int productId);
}
