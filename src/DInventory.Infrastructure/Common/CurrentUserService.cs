using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using Microsoft.AspNetCore.Http;

namespace DInventory.Infrastructure.Common;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CurrentUser GetCurrentUser()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        if (user?.Identity is null || !user.Identity.IsAuthenticated)
        {
            return new CurrentUser { IsAuthenticated = false };
        }

        var userIdClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var roleIdClaim = user.FindFirst("roleId")?.Value;

        return new CurrentUser
        {
            IsAuthenticated = true,
            UserId = int.TryParse(userIdClaim, out var userId) ? userId : 0,
            Username = user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty,
            FullName = user.FindFirst("fullName")?.Value ?? string.Empty,
            RoleId = int.TryParse(roleIdClaim, out var roleId) ? roleId : 0,
            RoleName = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty,
            IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString()
        };
    }
}
