namespace DInventory.Domain.Entities;

public class Subcategory
{
    public int SubcategoryId { get; set; }
    public int CategoryId { get; set; }
    public string SubcategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Which company this record belongs to. 0 = the built-in superuser company
    /// (bypasses company filtering everywhere); every other value is a real tenant.</summary>
    public int CompanyId { get; set; }
    public int? BusinessUnitId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    // Populated via join, not a DB column
    public string? CategoryName { get; set; }
}
