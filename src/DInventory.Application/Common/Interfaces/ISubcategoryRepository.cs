using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface ISubcategoryRepository
{
    Task<Subcategory?> GetByIdAsync(int subcategoryId);
    /// <summary>companyId = 0 (superuser) bypasses the filter and returns subcategories across every company.</summary>
    Task<IEnumerable<Subcategory>> GetAllAsync(int companyId, int? categoryId = null, string? search = null, bool onlyActive = false);
    Task<PagedResult<Subcategory>> GetPagedAsync(PagedRequest request, int companyId, int? categoryId = null, bool onlyActive = false);
    Task<int> CreateAsync(Subcategory subcategory);
    Task<bool> UpdateAsync(Subcategory subcategory);
    Task<bool> DeleteAsync(int subcategoryId);
    Task<bool> HasProductsAsync(int subcategoryId);
}
