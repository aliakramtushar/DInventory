namespace DInventory.Domain.Entities;

public class Size
{
    public int SizeId { get; set; }
    public string SizeName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    /// <summary>Which company this record belongs to. 0 = the built-in superuser company
    /// (bypasses company filtering everywhere); every other value is a real tenant.</summary>
    public int CompanyId { get; set; }
    public int? BusinessUnitId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
