using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Tenancy;

public interface IBusinessUnitService
{
    Task<BusinessUnit?> GetByIdAsync(int businessUnitId);
    Task<IEnumerable<BusinessUnit>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false);
    Task<PagedResult<BusinessUnit>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false);
    Task<Result<int>> CreateAsync(BusinessUnit businessUnit, int? actingUserId);
    Task<Result> UpdateAsync(BusinessUnit businessUnit, int? actingUserId);
    Task<Result> DeleteAsync(int businessUnitId);
}
