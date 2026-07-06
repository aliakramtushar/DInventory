using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Tenancy;

public interface ICompanyService
{
    Task<Company?> GetByIdAsync(int companyId);
    Task<IEnumerable<Company>> GetAllAsync(string? search = null, bool onlyActive = false);
    Task<PagedResult<Company>> GetPagedAsync(PagedRequest request, bool onlyActive = false);
    Task<Result<int>> CreateAsync(Company company, int? actingUserId);
    Task<Result> UpdateAsync(Company company, int? actingUserId);
    Task<Result> DeleteAsync(int companyId);
}
