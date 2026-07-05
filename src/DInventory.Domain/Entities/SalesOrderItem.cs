namespace DInventory.Domain.Entities;

public class SalesOrderItem
{
    public int SalesOrderItemId { get; set; }
    public int SalesOrderId { get; set; }
    public int ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>How this line's discount was entered (PERCENT or FIXED). DiscountAmount is the
    /// computed money value actually taken off; LineTotal = Quantity * UnitPrice - DiscountAmount.</summary>
    public string DiscountType { get; set; } = "PERCENT";
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }

    // Populated via joins, not DB columns
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public string? SizeName { get; set; }
    public string? Barcode { get; set; }

    /// <summary>Quantity already returned via SalesReturns against this line - not a DB column,
    /// joined in by the repository purely so the Return screen can show what's still returnable.</summary>
    public int ReturnedQuantity { get; set; }
}
