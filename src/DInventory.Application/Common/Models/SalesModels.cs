namespace DInventory.Application.Common.Models;

public class CreateSaleRequest
{
    public int? CustomerId { get; set; }
    public string? NewCustomerName { get; set; }
    public decimal DiscountAmount { get; set; }
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
}
