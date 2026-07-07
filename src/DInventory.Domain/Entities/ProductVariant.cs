namespace DInventory.Domain.Entities;

/// <summary>The actual sellable unit: one Product in one Size (and optionally one Color), identified
/// by its own unique barcode.</summary>
public class ProductVariant
{
    public int ProductVariantId { get; set; }
    public int ProductId { get; set; }
    public int SizeId { get; set; }
    public int? ColorId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? SKU { get; set; }
    public int ReorderLevel { get; set; } = 5;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }

    // Populated via joins, not DB columns
    public int CompanyId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public string? BrandName { get; set; }
    public string? CategoryName { get; set; }
    public string? SizeName { get; set; }
    public string? ColorName { get; set; }
    public decimal? SellingPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public int? QuantityOnHand { get; set; }
}
