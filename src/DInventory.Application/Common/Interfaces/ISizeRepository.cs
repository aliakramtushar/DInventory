using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface ISizeRepository
{
    Task<Size?> GetByIdAsync(int sizeId);
    Task<IEnumerable<Size>> GetAllAsync(bool onlyActive = false);
    Task<PagedResult<Size>> GetPagedAsync(PagedRequest request);
    Task<int> CreateAsync(Size size);
    Task<bool> UpdateAsync(Size size);
    Task<bool> DeleteAsync(int sizeId);
    Task<bool> NameExistsAsync(string name, int? excludeId = null);
    Task<bool> HasVariantsAsync(int sizeId);
}
