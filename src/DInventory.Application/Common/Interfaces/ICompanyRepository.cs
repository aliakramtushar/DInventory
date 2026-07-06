using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(int companyId);
    Task<IEnumerable<Company>> GetAllAsync(string? search = null, bool onlyActive = false);
    Task<PagedResult<Company>> GetPagedAsync(PagedRequest request, bool onlyActive = false);
    Task<int> CreateAsync(Company company);
    Task<bool> UpdateAsync(Company company);
    Task<bool> DeleteAsync(int companyId);
    Task<bool> HasDependentDataAsync(int companyId);
    Task<bool> NameExistsAsync(string name, int? excludeId = null);
    Task<bool> ShortNameExistsAsync(string shortName, int? excludeId = null);
}
