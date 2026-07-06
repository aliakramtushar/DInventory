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

    /// <summary>0 = the built-in superuser company - this user can see every company's data.
    /// Any other value scopes this user to that one company everywhere in the app.</summary>
    public int CompanyId { get; set; }
    public int? BusinessUnitId { get; set; }
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>True when CompanyId is 0 - the signal used everywhere to bypass company filtering,
    /// independent of role (a non-SuperAdmin role could in principle also be granted CompanyId 0,
    /// though today only the seeded SuperAdmin role is).</summary>
    public bool IsSuperCompany => CompanyId == 0;
    public bool IsAuthenticated { get; set; }
    public string? IpAddress { get; set; }
}
