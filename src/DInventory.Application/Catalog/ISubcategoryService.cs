using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public interface ISubcategoryService
{
    Task<Subcategory?> GetByIdAsync(int subcategoryId);
    Task<IEnumerable<Subcategory>> GetAllAsync(int companyId, int? categoryId = null, string? search = null, bool onlyActive = false);
    Task<PagedResult<Subcategory>> GetPagedAsync(PagedRequest request, int companyId, int? categoryId = null, bool onlyActive = false);
    Task<Result<int>> CreateAsync(Subcategory subcategory, int? actingUserId);
    Task<Result> UpdateAsync(Subcategory subcategory, int? actingUserId);
    Task<Result> DeleteAsync(int subcategoryId);
}
