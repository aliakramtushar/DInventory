namespace DInventory.Domain.Entities;

public class PurchaseReturnItem
{
    public int PurchaseReturnItemId { get; set; }
    public int PurchaseReturnId { get; set; }
    public int PurchaseItemId { get; set; }
    public int ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    // Populated via joins, not DB columns
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public string? SizeName { get; set; }
    public string? ColorName { get; set; }
    public string? Barcode { get; set; }
}
