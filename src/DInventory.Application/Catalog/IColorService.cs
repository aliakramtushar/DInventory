using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public interface IColorService
{
    Task<Color?> GetByIdAsync(int colorId);
    Task<IEnumerable<Color>> GetAllAsync(bool onlyActive = false);
    Task<PagedResult<Color>> GetPagedAsync(PagedRequest request);
    Task<Result<int>> CreateAsync(Color color, int? actingUserId);
    Task<Result> UpdateAsync(Color color, int? actingUserId);
    Task<Result> DeleteAsync(int colorId);
}
