using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public interface ISizeService
{
    Task<Size?> GetByIdAsync(int sizeId);
    Task<IEnumerable<Size>> GetAllAsync(int companyId, bool onlyActive = false, int? businessUnitId = null);
    Task<PagedResult<Size>> GetPagedAsync(PagedRequest request, int companyId, int? businessUnitId = null);
    Task<Result<int>> CreateAsync(Size size, int? actingUserId);
    Task<Result> UpdateAsync(Size size, int? actingUserId);
    Task<Result> DeleteAsync(int sizeId);
}
