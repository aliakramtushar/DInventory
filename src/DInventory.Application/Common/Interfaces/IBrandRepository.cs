using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IBrandRepository
{
    Task<Brand?> GetByIdAsync(int brandId);
    /// <summary>companyId = 0 (superuser) bypasses the filter and returns brands across every company.</summary>
    Task<IEnumerable<Brand>> GetAllAsync(int companyId, bool onlyActive = false, int? businessUnitId = null);
    Task<PagedResult<Brand>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false, int? businessUnitId = null);
    Task<int> CreateAsync(Brand brand);
    Task<bool> UpdateAsync(Brand brand);
    Task<bool> DeleteAsync(int brandId);
    Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null);
    Task<bool> HasProductsAsync(int brandId);
}
