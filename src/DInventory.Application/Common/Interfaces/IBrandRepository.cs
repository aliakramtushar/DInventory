using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IBrandRepository
{
    Task<Brand?> GetByIdAsync(int brandId);
    Task<IEnumerable<Brand>> GetAllAsync(bool onlyActive = false);
    Task<PagedResult<Brand>> GetPagedAsync(PagedRequest request, bool onlyActive = false);
    Task<int> CreateAsync(Brand brand);
    Task<bool> UpdateAsync(Brand brand);
    Task<bool> DeleteAsync(int brandId);
    Task<bool> NameExistsAsync(string name, int? excludeId = null);
    Task<bool> HasProductsAsync(int brandId);
}
