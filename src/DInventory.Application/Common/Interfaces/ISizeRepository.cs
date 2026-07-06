using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface ISizeRepository
{
    Task<Size?> GetByIdAsync(int sizeId);
    /// <summary>companyId = 0 (superuser) bypasses the filter and returns sizes across every company.</summary>
    Task<IEnumerable<Size>> GetAllAsync(int companyId, bool onlyActive = false);
    Task<PagedResult<Size>> GetPagedAsync(PagedRequest request, int companyId);
    Task<int> CreateAsync(Size size);
    Task<bool> UpdateAsync(Size size);
    Task<bool> DeleteAsync(int sizeId);
    Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null);
    Task<bool> HasVariantsAsync(int sizeId);
}
