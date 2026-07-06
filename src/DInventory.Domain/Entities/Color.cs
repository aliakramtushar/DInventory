namespace DInventory.Domain.Entities;

public class Color
{
    public int ColorId { get; set; }
    public string ColorName { get; set; } = string.Empty;
    public string? HexCode { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Which company this record belongs to. 0 = the built-in superuser company
    /// (bypasses company filtering everywhere); every other value is a real tenant.</summary>
    public int CompanyId { get; set; }
    public int? BusinessUnitId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
