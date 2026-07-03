namespace DInventory.Application.Common.Models;

public class CurrentUser
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsSuperAdmin => RoleName.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);
    public bool IsAdmin => RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    public bool IsAuthenticated { get; set; }
    public string? IpAddress { get; set; }
}
