using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public interface IBrandService
{
    Task<Brand?> GetByIdAsync(int brandId);
    Task<IEnumerable<Brand>> GetAllAsync(bool onlyActive = false);
    Task<PagedResult<Brand>> GetPagedAsync(PagedRequest request, bool onlyActive = false);
    Task<Result<int>> CreateAsync(Brand brand, int? actingUserId);
    Task<Result> UpdateAsync(Brand brand, int? actingUserId);
    Task<Result> DeleteAsync(int brandId);
}
