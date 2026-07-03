using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Purchasing;

public class CreatePurchaseItemInput
{
    public int ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public decimal BuyingPrice { get; set; }
}

public class CreatePurchaseRequest
{
    public int SupplierId { get; set; }
    public decimal PaidAmount { get; set; }
    public string? Remarks { get; set; }
    public List<CreatePurchaseItemInput> Items { get; set; } = new();
}

public interface IPurchaseService
{
    Task<Purchase?> GetByIdAsync(int purchaseId);
    Task<PagedResult<Purchase>> GetPagedAsync(PagedRequest request, int? supplierId = null);

    /// <summary>Records a purchase invoice and immediately increases stock for every line item (one
    /// StockTransaction per variant, ReferenceType = "PURCHASE") - the "Auto Stock Update" the Phase 1
    /// spec asks for, using the exact same stock-adjustment plumbing sales/manual adjustments use.</summary>
    Task<Result<int>> CreateAsync(CreatePurchaseRequest request, int actingUserId);
}
