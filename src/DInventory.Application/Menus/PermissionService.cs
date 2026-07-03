using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace DInventory.Application.Menus;

public class PermissionService : IPermissionService
{
    private readonly IRoleMenuPermissionRepository _permissionRepository;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public PermissionService(IRoleMenuPermissionRepository permissionRepository, IMemoryCache cache)
    {
        _permissionRepository = permissionRepository;
        _cache = cache;
    }

    public async Task<IEnumerable<MenuPermissionDto>> GetPermissionsForRoleAsync(int roleId)
    {
        var cacheKey = CacheKey(roleId);
        if (_cache.TryGetValue(cacheKey, out List<MenuPermissionDto>? cached) && cached is not null)
        {
            return cached;
        }

        var permissions = (await _permissionRepository.GetPermissionsForRoleAsync(roleId)).ToList();
        _cache.Set(cacheKey, permissions, CacheDuration);
        return permissions;
    }

    public async Task<IEnumerable<MenuPermissionDto>> GetAccessibleMenuTreeAsync(int roleId, bool isSuperAdmin)
    {
        var permissions = (await GetPermissionsForRoleAsync(roleId)).ToList();
        if (isSuperAdmin)
        {
            // SuperAdmin sees every menu regardless of stored permission rows
            return permissions.Select(p => new MenuPermissionDto
            {
                MenuId = p.MenuId,
                MenuKey = p.MenuKey,
                MenuName = p.MenuName,
                Icon = p.Icon,
                Url = p.Url,
                ParentId = p.ParentId,
                DisplayOrder = p.DisplayOrder,
                CanView = true,
                CanCreate = true,
                CanEdit = true,
                CanDelete = true
            });
        }

        return permissions.Where(p => p.CanView);
    }

    public async Task<bool> HasPermissionAsync(int roleId, bool isSuperAdmin, string menuKey, PermissionAction action)
    {
        if (isSuperAdmin)
        {
            return true;
        }

        var permissions = await GetPermissionsForRoleAsync(roleId);
        var permission = permissions.FirstOrDefault(p => string.Equals(p.MenuKey, menuKey, StringComparison.OrdinalIgnoreCase));
        if (permission is null)
        {
            return false;
        }

        return action switch
        {
            PermissionAction.View => permission.CanView,
            PermissionAction.Create => permission.CanCreate,
            PermissionAction.Edit => permission.CanEdit,
            PermissionAction.Delete => permission.CanDelete,
            _ => false
        };
    }

    public async Task SaveRolePermissionsAsync(int roleId, IEnumerable<RolePermissionUpdateItem> items)
    {
        await _permissionRepository.SaveRolePermissionsAsync(roleId, items);
        InvalidateCache(roleId);
    }

    public void InvalidateCache(int roleId) => _cache.Remove(CacheKey(roleId));

    private static string CacheKey(int roleId) => $"role-permissions-{roleId}";
}
