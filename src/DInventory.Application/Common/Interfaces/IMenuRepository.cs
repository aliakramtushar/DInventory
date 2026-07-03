using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IMenuRepository
{
    Task<Menu?> GetByIdAsync(int menuId);
    Task<Menu?> GetByKeyAsync(string menuKey);
    Task<IEnumerable<Menu>> GetAllAsync();
    Task<PagedResult<Menu>> GetPagedAsync(PagedRequest request);
    Task<int> CreateAsync(Menu menu);
    Task<bool> UpdateAsync(Menu menu);
    Task<bool> DeleteAsync(int menuId);
}
