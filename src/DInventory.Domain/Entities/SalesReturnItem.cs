namespace DInventory.Domain.Entities;

public class SalesReturnItem
{
    public int SalesReturnItemId { get; set; }
    public int SalesReturnId { get; set; }
    public int SalesOrderItemId { get; set; }
    public int ProductVariantId { get; set; }
    public int Quantity { get; set; }

    /// <summary>The original sale line's effective (post-discount) per-unit price, so the refund
    /// automatically reflects whatever discount was already given at sale time.</summary>
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    // Populated via joins, not DB columns
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public string? SizeName { get; set; }
    public string? Barcode { get; set; }
}
