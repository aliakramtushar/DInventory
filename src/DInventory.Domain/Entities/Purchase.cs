namespace DInventory.Domain.Entities;

/// <summary>A purchase invoice from a supplier. Receiving it increases stock the same way a sale
/// decreases it (see StockTransactions.ReferenceType = "PURCHASE").</summary>
public class Purchase
{
    public int PurchaseId { get; set; }
    public string PurchaseInvoiceNo { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    /// <summary>Which company this record belongs to. 0 = the built-in superuser company
    /// (bypasses company filtering everywhere); every other value is a real tenant.</summary>
    public int CompanyId { get; set; }
    public int? BusinessUnitId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }

    // Populated via joins, not DB columns
    public string? SupplierName { get; set; }
    public string? CreatedByName { get; set; }
    public string? CompanyName { get; set; }

    /// <summary>Sum of TotalAmount across all PurchaseReturns filed against this purchase - not a
    /// DB column, joined in by the repository. Reduces what's actually owed to the supplier
    /// without mutating the original invoice's TotalAmount (keeps the original invoice intact for
    /// audit purposes; the return is its own separate, traceable record).</summary>
    public decimal ReturnedAmount { get; set; }
    public decimal DueAmount => TotalAmount - PaidAmount - ReturnedAmount;
    public List<PurchaseItem> Items { get; set; } = new();
}
