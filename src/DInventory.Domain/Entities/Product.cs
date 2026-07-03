namespace DInventory.Domain.Entities;

/// <summary>A style/product line (e.g. "Classic Crew T-Shirt"). Not directly sellable - see
/// <see cref="ProductVariant"/> for the actual per-size sellable unit with its own barcode/stock.</summary>
public class Product
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int? SubcategoryId { get; set; }
    public int? BrandId { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public int ReorderLevel { get; set; } = 5;
    public bool IsShowOnWebsite { get; set; }
    public bool ShowPriceOnWebsite { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    // Populated via joins, not DB columns
    public string? CategoryName { get; set; }
    public string? SubcategoryName { get; set; }
    public string? BrandName { get; set; }
    public decimal? SellingPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public int? TotalQuantityOnHand { get; set; }
    public int VariantCount { get; set; }
}
