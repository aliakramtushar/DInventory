namespace DInventory.Domain.Entities;

/// <summary>An optional sub-division within a Company (e.g. a branch/outlet). Nullable everywhere
/// it's referenced - a company that doesn't need business units can just leave it blank, same as
/// ProductVariants.ColorId being optional.</summary>
public class BusinessUnit
{
    public int BusinessUnitId { get; set; }
    public int CompanyId { get; set; }
    public string BusinessUnitName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    // Populated via join, not a DB column
    public string? CompanyName { get; set; }
}
