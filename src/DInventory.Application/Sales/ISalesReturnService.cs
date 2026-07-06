using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Sales;

public class CreateSalesReturnItemInput
{
    public int SalesOrderItemId { get; set; }
    public int Quantity { get; set; }
}

public class CreateSalesReturnRequest
{
    public int SalesOrderId { get; set; }
    public string? Reason { get; set; }
    public string? Remarks { get; set; }
    public List<CreateSalesReturnItemInput> Items { get; set; } = new();
}

public interface ISalesReturnService
{
    Task<SalesReturn?> GetByIdAsync(int salesReturnId);
    Task<PagedResult<SalesReturn>> GetPagedAsync(PagedRequest request, int companyId, int? salesOrderId = null);

    /// <summary>Records a return against a completed sale (always linked to the original
    /// SalesOrder) and immediately restores stock for every returned line, one StockTransaction
    /// per variant with ReferenceType = "SALE_RETURN" - the same stock-adjustment plumbing
    /// sales/purchases/manual adjustments use. Each line's refund uses the ORIGINAL sale line's
    /// effective (post-discount) per-unit price, so any discount already given is honored, and
    /// quantity is validated against what's still returnable (sold qty minus already-returned qty
    /// across any prior returns on that same line).</summary>
    Task<Result<int>> CreateReturnAsync(CreateSalesReturnRequest request, int actingUserId);
}
