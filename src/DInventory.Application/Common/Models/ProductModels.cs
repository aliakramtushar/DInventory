namespace DInventory.Application.Common.Models;

/// <summary>One size/variant to create together with a new product (fashion house concept: a
/// product/style is created once, then stocked in one or more sizes/colors, each with its own
/// barcode).</summary>
public class ProductVariantInput
{
    public int SizeId { get; set; }
    public int? ColorId { get; set; }

    /// <summary>Leave blank to auto-generate the next barcode in DInventory's own series.</summary>
    public string? Barcode { get; set; }

    /// <summary>Leave blank to auto-generate as "{ProductCode}-{SizeName}[-{ColorName}]".</summary>
    public string? SKU { get; set; }
    public int ReorderLevel { get; set; } = 5;
    public int InitialQuantity { get; set; }
}
