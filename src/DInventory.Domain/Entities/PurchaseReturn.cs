namespace DInventory.Domain.Entities;

/// <summary>A return of stock back to a supplier against an original purchase invoice, always
/// linked to it. Recording one reduces stock for every returned line
/// (StockTransactions.ReferenceType = "PURCHASE_RETURN").</summary>
public class PurchaseReturn
{
    public int PurchaseReturnId { get; set; }
    public string ReturnNo { get; set; } = string.Empty;
    public int PurchaseId { get; set; }
    /// <summary>Which company this record belongs to. 0 = the built-in superuser company
    /// (bypasses company filtering everywhere); every other value is a real tenant.</summary>
    public int CompanyId { get; set; }
    public int? BusinessUnitId { get; set; }
    public DateTime ReturnDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Reason { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }

    // Populated via joins, not DB columns
    public string? PurchaseInvoiceNo { get; set; }
    public string? SupplierName { get; set; }
    public string? CreatedByName { get; set; }
    public List<PurchaseReturnItem> Items { get; set; } = new();
}
