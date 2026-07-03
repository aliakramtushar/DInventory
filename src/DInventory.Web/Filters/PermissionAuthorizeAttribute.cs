using DInventory.Application.Common.Interfaces;
using DInventory.Application.Menus;
using DInventory.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DInventory.Web.Filters;

/// <summary>
/// Fine-grained, database-driven permission check (RoleMenuPermissions table). SuperAdmin bypasses everything.
/// Usage: [PermissionAuthorize("PRODUCTS", PermissionAction.Edit)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class PermissionAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _menuKey;
    private readonly PermissionAction _action;

    public PermissionAuthorizeAttribute(string menuKey, PermissionAction action = PermissionAction.View)
    {
        _menuKey = menuKey;
        _action = action;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var httpUser = context.HttpContext.User;
        if (httpUser.Identity is null || !httpUser.Identity.IsAuthenticated)
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl = context.HttpContext.Request.Path.ToString() });
            return;
        }

        var currentUserService = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();

        var currentUser = currentUserService.GetCurrentUser();
        var allowed = await permissionService.HasPermissionAsync(currentUser.RoleId, currentUser.IsSuperAdmin, _menuKey, _action);

        if (!allowed)
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
