namespace DInventory.Domain.Entities;

public class Stock
{
    public int StockId { get; set; }
    public int ProductVariantId { get; set; }
    public int QuantityOnHand { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Populated via joins, not DB columns
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public string? BrandName { get; set; }
    public string? SizeName { get; set; }
    public string? Barcode { get; set; }
    public int? ReorderLevel { get; set; }
}
