namespace DInventory.Domain.Entities;

public class SalesOrder
{
    public int SalesOrderId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal SubTotal { get; set; }
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
    public List<SalesOrderItem> Items { get; set; } = new();
}
