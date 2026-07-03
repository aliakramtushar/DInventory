using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Menus;

public interface IMenuService
{
    Task<Menu?> GetByIdAsync(int menuId);
    Task<IEnumerable<Menu>> GetAllAsync();
    Task<PagedResult<Menu>> GetPagedAsync(PagedRequest request);
    Task<Result<int>> CreateAsync(Menu menu);
    Task<Result> UpdateAsync(Menu menu);
    Task<Result> DeleteAsync(int menuId);
}
