using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Purchasing;

public class CreatePurchaseReturnItemInput
{
    public int PurchaseItemId { get; set; }
    public int Quantity { get; set; }
}

public class CreatePurchaseReturnRequest
{
    public int PurchaseId { get; set; }
    public string? Reason { get; set; }
    public string? Remarks { get; set; }
    public List<CreatePurchaseReturnItemInput> Items { get; set; } = new();
}

public interface IPurchaseReturnService
{
    Task<PurchaseReturn?> GetByIdAsync(int purchaseReturnId);
    Task<PagedResult<PurchaseReturn>> GetPagedAsync(PagedRequest request, int companyId, int? purchaseId = null);

    /// <summary>Records a return of stock back to the supplier against an original purchase
    /// (always linked to it) and immediately reduces stock for every returned line, one
    /// StockTransaction per variant with ReferenceType = "PURCHASE_RETURN". Quantity is validated
    /// against both what's still returnable on that line (received qty minus already-returned
    /// qty) and current stock on hand (can't return more than is physically in stock).</summary>
    Task<Result<int>> CreateReturnAsync(CreatePurchaseReturnRequest request, int actingUserId);
}
