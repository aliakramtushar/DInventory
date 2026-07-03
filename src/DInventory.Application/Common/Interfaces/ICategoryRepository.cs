using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(int categoryId);
    Task<IEnumerable<Category>> GetAllAsync(string? search = null, bool onlyActive = false);
    Task<PagedResult<Category>> GetPagedAsync(PagedRequest request, bool onlyActive = false);
    Task<int> CreateAsync(Category category);
    Task<bool> UpdateAsync(Category category);
    Task<bool> DeleteAsync(int categoryId);
    Task<bool> HasProductsAsync(int categoryId);
    Task<bool> NameExistsAsync(string name, int? excludeId = null);
}
