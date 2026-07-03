namespace DInventory.Domain.Entities;

public class Color
{
    public int ColorId { get; set; }
    public string ColorName { get; set; } = string.Empty;
    public string? HexCode { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
