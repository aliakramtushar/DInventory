using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IColorRepository
{
    Task<Color?> GetByIdAsync(int colorId);
    /// <summary>companyId = 0 (superuser) bypasses the filter and returns colors across every company.</summary>
    Task<IEnumerable<Color>> GetAllAsync(int companyId, bool onlyActive = false);
    Task<PagedResult<Color>> GetPagedAsync(PagedRequest request, int companyId);
    Task<int> CreateAsync(Color color);
    Task<bool> UpdateAsync(Color color);
    Task<bool> DeleteAsync(int colorId);
    Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null);
    Task<bool> HasVariantsAsync(int colorId);
}
