using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IPurchaseReturnRepository
{
    Task<PurchaseReturn?> GetByIdAsync(int purchaseReturnId);
    Task<PagedResult<PurchaseReturn>> GetPagedAsync(PagedRequest request, int companyId, int? purchaseId = null);
    Task<int> CreateAsync(PurchaseReturn purchaseReturn);
    Task<string> GenerateNextReturnNoAsync();
}
