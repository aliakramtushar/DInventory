namespace DInventory.Domain.Entities;

/// <summary>A purchase invoice from a supplier. Receiving it increases stock the same way a sale
/// decreases it (see StockTransactions.ReferenceType = "PURCHASE").</summary>
public class Purchase
{
    public int PurchaseId { get; set; }
    public string PurchaseInvoiceNo { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }

    // Populated via joins, not DB columns
    public string? SupplierName { get; set; }
    public string? CreatedByName { get; set; }
    public decimal DueAmount => TotalAmount - PaidAmount;
    public List<PurchaseItem> Items { get; set; } = new();
}
