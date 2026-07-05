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
    public string? CompanyName { get; set; }
    public decimal? Price { get; set; }

    /// <summary>CODE128 module (bar) width in px, as printed. Default 2.</summary>
    public int BarcodeWidth { get; set; } = 2;

    /// <summary>Barcode height in px, as printed. Default 50.</summary>
    public int BarcodeHeight { get; set; } = 50;
    public bool IsLinked { get; set; }
    public int? LinkedProductVariantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
}
