using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface ISalesReturnRepository
{
    Task<SalesReturn?> GetByIdAsync(int salesReturnId);
    Task<PagedResult<SalesReturn>> GetPagedAsync(PagedRequest request, int? salesOrderId = null);
    Task<int> CreateAsync(SalesReturn salesReturn);
    Task<string> GenerateNextReturnNoAsync();
}
