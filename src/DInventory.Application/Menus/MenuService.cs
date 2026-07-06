using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Menus;

public class MenuService : IMenuService
{
    private readonly IMenuRepository _menuRepository;

    public MenuService(IMenuRepository menuRepository)
    {
        _menuRepository = menuRepository;
    }

    public Task<Menu?> GetByIdAsync(int menuId) => _menuRepository.GetByIdAsync(menuId);

    public Task<IEnumerable<Menu>> GetAllAsync() => _menuRepository.GetAllAsync();

    public Task<PagedResult<Menu>> GetPagedAsync(PagedRequest request) => _menuRepository.GetPagedAsync(request);

    public async Task<Result<int>> CreateAsync(Menu menu)
    {
        var existing = await _menuRepository.GetByKeyAsync(menu.MenuKey);
        if (existing is not null)
        {
            return Result<int>.Failure("A menu with this key already exists.");
        }

        try
        {
            var id = await _menuRepository.CreateAsync(menu);
            return Result<int>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save menu: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(Menu menu)
    {
        try
        {
            var ok = await _menuRepository.UpdateAsync(menu);
            return ok ? Result.Success() : Result.Failure("Unable to update menu.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update menu: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(int menuId)
    {
        try
        {
            var ok = await _menuRepository.DeleteAsync(menuId);
            return ok ? Result.Success() : Result.Failure("Unable to delete menu. It may still have role permissions or submenus attached.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to delete menu item: {ex.Message}");
        }
    }
}
