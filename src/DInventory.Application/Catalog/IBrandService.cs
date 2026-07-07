using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public interface IBrandService
{
    Task<Brand?> GetByIdAsync(int brandId);
    Task<IEnumerable<Brand>> GetAllAsync(int companyId, bool onlyActive = false, int? businessUnitId = null);
    Task<PagedResult<Brand>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false, int? businessUnitId = null);
    Task<Result<int>> CreateAsync(Brand brand, int? actingUserId);
    Task<Result> UpdateAsync(Brand brand, int? actingUserId);
    Task<Result> DeleteAsync(int brandId);
}
