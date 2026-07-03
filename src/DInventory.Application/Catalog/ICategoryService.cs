using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public interface ICategoryService
{
    Task<Category?> GetByIdAsync(int categoryId);
    Task<IEnumerable<Category>> GetAllAsync(string? search = null, bool onlyActive = false);
    Task<PagedResult<Category>> GetPagedAsync(PagedRequest request, bool onlyActive = false);
    Task<Result<int>> CreateAsync(Category category, int? actingUserId);
    Task<Result> UpdateAsync(Category category, int? actingUserId);
    Task<Result> DeleteAsync(int categoryId);
}
