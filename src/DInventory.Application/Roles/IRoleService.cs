using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Roles;

public interface IRoleService
{
    Task<Role?> GetByIdAsync(int roleId);
    Task<IEnumerable<Role>> GetAllAsync();
    Task<PagedResult<Role>> GetPagedAsync(PagedRequest request);
    Task<Result<int>> CreateAsync(Role role);
    Task<Result> UpdateAsync(Role role);
    Task<Result> DeleteAsync(int roleId);
}
