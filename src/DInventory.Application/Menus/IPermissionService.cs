using DInventory.Application.Common.Models;
using DInventory.Domain.Enums;

namespace DInventory.Application.Menus;

public interface IPermissionService
{
    Task<IEnumerable<MenuPermissionDto>> GetPermissionsForRoleAsync(int roleId);
    Task<IEnumerable<MenuPermissionDto>> GetAccessibleMenuTreeAsync(int roleId, bool isSuperAdmin);
    Task<bool> HasPermissionAsync(int roleId, bool isSuperAdmin, string menuKey, PermissionAction action);
    Task SaveRolePermissionsAsync(int roleId, IEnumerable<RolePermissionUpdateItem> items);
    void InvalidateCache(int roleId);
}
