namespace DInventory.Domain.Entities;

public class SalesOrder
{
    public int SalesOrderId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal SubTotal { get; set; }

    /// <summary>How the overall bill discount was entered (PERCENT or FIXED) - kept alongside
    /// DiscountAmount purely for display/edit; DiscountAmount below is the combined (line +
    /// bill) computed money figure everything else (NetAmount, reports) already reads.</summary>
    public string DiscountType { get; set; } = "FIXED";
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string PaymentStatus { get; set; } = "PAID"; // PAID, DUE, PARTIAL
    public string PaymentMethod { get; set; } = "CASH"; // CASH, CARD, MOBILE_BANKING, DUE
    public string Status { get; set; } = "COMPLETED";   // COMPLETED, CANCELLED
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }

    // Populated via joins, not DB columns
    public string? CustomerName { get; set; }
    public string? CreatedByName { get; set; }

    /// <summary>Sum of NetAmount across all SalesReturns filed against this order - not a DB
    /// column, joined in by the repository purely for display ("Net after returns").</summary>
    public decimal ReturnedAmount { get; set; }
    public List<SalesOrderItem> Items { get; set; } = new();
}
