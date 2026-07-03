namespace DInventory.Domain.Entities;

/// <summary>A barcode + label printed ahead of formal product entry (tag first, enter into the
/// system later). Kept as a log so a printed barcode can be looked up/reused during Product entry.</summary>
public class GeneratedBarcodeLabel
{
    public int LabelId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string? SizeName { get; set; }
    public decimal? Price { get; set; }
    public bool IsLinked { get; set; }
    public int? LinkedProductVariantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
}
