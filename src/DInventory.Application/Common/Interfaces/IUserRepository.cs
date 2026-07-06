using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllAsync(string? search = null);
    Task<PagedResult<User>> GetPagedAsync(PagedRequest request, int? companyId = null, int? businessUnitId = null);
    Task<int> CreateAsync(User user);
    Task<bool> UpdateAsync(User user);
    Task<bool> DeleteAsync(int userId);
    Task<bool> SetActiveAsync(int userId, bool isActive);
    Task<bool> UpdatePasswordAsync(int userId, string passwordHash);
    Task<bool> UpdateLastLoginAsync(int userId);
    Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null);
    Task<bool> EmailExistsAsync(string email, int? excludeUserId = null);
}
