using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Users;

public interface IUserService
{
    Task<User?> GetByIdAsync(int userId);
    Task<IEnumerable<User>> GetAllAsync(string? search = null);
    Task<PagedResult<User>> GetPagedAsync(PagedRequest request);
    Task<Result<int>> CreateAsync(User user, string password, int? actingUserId);
    Task<Result> UpdateAsync(User user, int? actingUserId);
    Task<Result> DeleteAsync(int userId, int actingUserId);
    Task<Result> ToggleActiveAsync(int userId, bool isActive);
    Task<Result> AdminResetPasswordAsync(int userId, string newPassword);
}
