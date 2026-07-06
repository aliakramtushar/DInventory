using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IBusinessUnitRepository
{
    Task<BusinessUnit?> GetByIdAsync(int businessUnitId);
    Task<IEnumerable<BusinessUnit>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false);
    Task<PagedResult<BusinessUnit>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false);
    Task<int> CreateAsync(BusinessUnit businessUnit);
    Task<bool> UpdateAsync(BusinessUnit businessUnit);
    Task<bool> DeleteAsync(int businessUnitId);
    Task<bool> HasDependentDataAsync(int businessUnitId);
    Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null);
}
