using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IRoleMenuPermissionRepository
{
    Task<IEnumerable<MenuPermissionDto>> GetPermissionsForRoleAsync(int roleId);
    Task<RoleMenuPermission?> GetAsync(int roleId, int menuId);
    Task SaveRolePermissionsAsync(int roleId, IEnumerable<RolePermissionUpdateItem> items);
}
