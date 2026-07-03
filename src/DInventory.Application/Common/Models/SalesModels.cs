namespace DInventory.Application.Common.Models;

public class CreateSaleRequest
{
    public int? CustomerId { get; set; }
    public string? NewCustomerName { get; set; }

    /// <summary>Overall bill discount, entered as either "PERCENT" (of the post-line-discount
    /// subtotal) or "FIXED" (a flat amount). The actual money value is computed server-side in
    /// SalesService and stored on SalesOrder.DiscountAmount together with the line discounts.</summary>
    public string DiscountType { get; set; } = "FIXED";
    public decimal DiscountValue { get; set; }
    public decimal TaxAmount { get; set; }
    public string PaymentStatus { get; set; } = "PAID";
    public string PaymentMethod { get; set; } = "CASH";
    public string? Remarks { get; set; }
    public List<CreateSaleItem> Items { get; set; } = new();
}

public class CreateSaleItem
{
    public int ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Per-product discount, entered as either "PERCENT" (of Quantity * UnitPrice) or a
    /// "FIXED" manual amount.</summary>
    public string DiscountType { get; set; } = "FIXED";
    public decimal DiscountValue { get; set; }
}
