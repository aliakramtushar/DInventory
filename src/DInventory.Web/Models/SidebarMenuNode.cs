using DInventory.Application.Common.Models;

namespace DInventory.Web.Models;

/// <summary>A top-level sidebar entry (either a real link like Dashboard, or one of the seeded
/// GROUP_* headers) together with its children for the two-level collapsible nav.</summary>
public class SidebarMenuNode
{
    public MenuPermissionDto Menu { get; set; } = new();
    public List<MenuPermissionDto> Children { get; set; } = new();
    public bool IsActive { get; set; }
    public bool HasChildren => Children.Count > 0;
}
