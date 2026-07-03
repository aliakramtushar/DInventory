namespace DInventory.Domain.Entities;

public class RoleMenuPermission
{
    public int PermissionId { get; set; }
    public int RoleId { get; set; }
    public int MenuId { get; set; }
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }

    // Populated via join, not a DB column
    public string? MenuKey { get; set; }
    public string? MenuName { get; set; }
}
