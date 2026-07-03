using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Menus;
using DInventory.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.ViewComponents;

public class SidebarMenuViewComponent : ViewComponent
{
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUserService;

    public SidebarMenuViewComponent(IPermissionService permissionService, ICurrentUserService currentUserService)
    {
        _permissionService = permissionService;
        _currentUserService = currentUserService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsAuthenticated)
        {
            return View(new List<SidebarMenuNode>());
        }

        var menus = (await _permissionService.GetAccessibleMenuTreeAsync(currentUser.RoleId, currentUser.IsSuperAdmin)).ToList();

        // Build a two-level tree: top-level items (ParentId == null, including the seeded
        // GROUP_ADMIN/GROUP_SALES/GROUP_REPORTS/GROUP_CATALOG headers) with their children nested
        // underneath. Plain top-level items with no children (e.g. Dashboard) render as a normal link;
        // items that do have children render as a collapsible group.
        var childrenByParent = menus
            .Where(m => m.ParentId.HasValue)
            .GroupBy(m => m.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.DisplayOrder).ToList());

        var currentController = ViewContext.RouteData.Values["controller"]?.ToString() ?? "";

        var tree = menus
            .Where(m => m.ParentId == null)
            .OrderBy(m => m.DisplayOrder)
            .Select(top =>
            {
                var children = childrenByParent.TryGetValue(top.MenuId, out var kids) ? kids : new List<MenuPermissionDto>();
                var node = new SidebarMenuNode
                {
                    Menu = top,
                    Children = children
                };

                // A group is "active" (expanded + highlighted) if the current controller matches
                // the group's own URL or any of its children's URLs.
                node.IsActive = IsMenuActive(top, currentController) || children.Any(c => IsMenuActive(c, currentController));
                return node;
            })
            .ToList();

        return View(tree);
    }

    private static bool IsMenuActive(MenuPermissionDto menu, string currentController)
        => string.Equals(menu.Url?.Trim('/'), currentController, StringComparison.OrdinalIgnoreCase);
}
