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
    public string DiscountType { get; set; } = "PERCENT";
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string PaymentStatus { get; set; } = "PAID"; // PAID, DUE, PARTIAL
    public string PaymentMethod { get; set; } = "CASH"; // CASH, CARD, MOBILE_BANKING, DUE
    public string Status { get; set; } = "COMPLETED";   // COMPLETED, CANCELLED
    public string? Remarks { get; set; }

    /// <summary>Loyalty points awarded to the customer for this sale (0 for walk-in sales or
    /// while the loyalty program is off/unconfigured).</summary>
    public int LoyaltyPointsEarned { get; set; }

    /// <summary>Loyalty points the customer redeemed against this sale's bill.</summary>
    public int LoyaltyPointsRedeemed { get; set; }

    /// <summary>Money value of LoyaltyPointsRedeemed (already netted into DiscountAmount/NetAmount
    /// above) - kept separately purely so Sales/Details can show it as its own line.</summary>
    public decimal LoyaltyRedeemAmount { get; set; }
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
