using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(int categoryId);
    Task<IEnumerable<Category>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false, int? businessUnitId = null);
    Task<PagedResult<Category>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false, int? businessUnitId = null);
    Task<int> CreateAsync(Category category);
    Task<bool> UpdateAsync(Category category);
    Task<bool> DeleteAsync(int categoryId);
    Task<bool> HasProductsAsync(int categoryId);
    Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null);
}
