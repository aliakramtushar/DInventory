namespace DInventory.Domain.Entities;

public class Size
{
    public int SizeId { get; set; }
    public string SizeName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
