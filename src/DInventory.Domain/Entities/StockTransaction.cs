namespace DInventory.Domain.Entities;

public class StockTransaction
{
    public int TransactionId { get; set; }
    public int ProductVariantId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // IN, OUT, ADJUSTMENT
    public int Quantity { get; set; }
    public string? ReferenceType { get; set; } // SALE, PURCHASE, MANUAL
    public int? ReferenceId { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }

    // Populated via joins, not DB columns
    public string? ProductName { get; set; }
    public string? SizeName { get; set; }
    public string? Barcode { get; set; }
    public string? CreatedByName { get; set; }
}
