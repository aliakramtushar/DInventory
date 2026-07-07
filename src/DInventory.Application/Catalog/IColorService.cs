using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public interface IColorService
{
    Task<Color?> GetByIdAsync(int colorId);
    Task<IEnumerable<Color>> GetAllAsync(int companyId, bool onlyActive = false, int? businessUnitId = null);
    Task<PagedResult<Color>> GetPagedAsync(PagedRequest request, int companyId, int? businessUnitId = null);
    Task<Result<int>> CreateAsync(Color color, int? actingUserId);
    Task<Result> UpdateAsync(Color color, int? actingUserId);
    Task<Result> DeleteAsync(int colorId);
}
