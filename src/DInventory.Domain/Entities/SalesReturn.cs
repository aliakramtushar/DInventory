namespace DInventory.Domain.Entities;

/// <summary>A return/credit against a completed sale, always linked to the original SalesOrder.
/// Recording one restores stock for every returned line (StockTransactions.ReferenceType =
/// "SALE_RETURN").</summary>
public class SalesReturn
{
    public int SalesReturnId { get; set; }
    public string ReturnNo { get; set; } = string.Empty;
    public int SalesOrderId { get; set; }
    /// <summary>Which company this record belongs to. 0 = the built-in superuser company
    /// (bypasses company filtering everywhere); every other value is a real tenant.</summary>
    public int CompanyId { get; set; }
    public int? BusinessUnitId { get; set; }
    public DateTime ReturnDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal NetAmount { get; set; }
    public string? Reason { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }

    // Populated via joins, not DB columns
    public string? InvoiceNo { get; set; }
    public string? CustomerName { get; set; }
    public string? CreatedByName { get; set; }
    public List<SalesReturnItem> Items { get; set; } = new();
}
