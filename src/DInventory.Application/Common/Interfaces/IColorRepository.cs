using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IColorRepository
{
    Task<Color?> GetByIdAsync(int colorId);
    Task<IEnumerable<Color>> GetAllAsync(bool onlyActive = false);
    Task<PagedResult<Color>> GetPagedAsync(PagedRequest request);
    Task<int> CreateAsync(Color color);
    Task<bool> UpdateAsync(Color color);
    Task<bool> DeleteAsync(int colorId);
    Task<bool> NameExistsAsync(string name, int? excludeId = null);
    Task<bool> HasVariantsAsync(int colorId);
}
