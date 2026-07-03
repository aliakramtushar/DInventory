namespace DInventory.Domain.Entities;

public class Subcategory
{
    public int SubcategoryId { get; set; }
    public int CategoryId { get; set; }
    public string SubcategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    // Populated via join, not a DB column
    public string? CategoryName { get; set; }
}
