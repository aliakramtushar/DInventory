using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public UserService(IUserRepository userRepository, IPasswordHasher passwordHasher, IRefreshTokenRepository refreshTokenRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public Task<User?> GetByIdAsync(int userId) => _userRepository.GetByIdAsync(userId);

    public Task<IEnumerable<User>> GetAllAsync(string? search = null) => _userRepository.GetAllAsync(search);

    public Task<PagedResult<User>> GetPagedAsync(PagedRequest request, int? companyId = null, int? businessUnitId = null) => _userRepository.GetPagedAsync(request, companyId, businessUnitId);

    public async Task<Result<int>> CreateAsync(User user, string password, int? actingUserId)
    {
        if (await _userRepository.UsernameExistsAsync(user.Username))
        {
            return Result<int>.Failure("Username is already taken.");
        }

        if (await _userRepository.EmailExistsAsync(user.Email))
        {
            return Result<int>.Failure("Email is already registered.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 5)
        {
            return Result<int>.Failure("Password must be at least 5 characters long.");
        }

        if (user.CompanyId < 0)
        {
            return Result<int>.Failure("Choose which company this user belongs to.");
        }

        user.PasswordHash = _passwordHasher.Hash(password);
        user.CreatedBy = actingUserId;
        user.CreatedAt = DateTime.UtcNow;
        user.IsActive = true;

        try
        {
            var id = await _userRepository.CreateAsync(user);
            return Result<int>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save user: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(User user, int? actingUserId)
    {
        var existing = await _userRepository.GetByIdAsync(user.UserId);
        if (existing is null)
        {
            return Result.Failure("User not found.");
        }

        if (await _userRepository.UsernameExistsAsync(user.Username, user.UserId))
        {
            return Result.Failure("Username is already taken.");
        }

        if (await _userRepository.EmailExistsAsync(user.Email, user.UserId))
        {
            return Result.Failure("Email is already registered.");
        }

        existing.Username = user.Username;
        existing.Email = user.Email;
        existing.FullName = user.FullName;
        existing.RoleId = user.RoleId;

        // BusinessUnitId must always depend on CompanyId - it's only ever assigned at Create time
        // (from the effective company's own business units) and is otherwise immutable, same as
        // every other entity in the app. If the company itself isn't changing, ignore whatever
        // BusinessUnitId came in on the posted form entirely (a company-scoped admin's hidden field
        // could be tampered with to reference a business unit belonging to a different company) and
        // keep the user's existing one. If a SuperAdmin *is* moving this user to a different company,
        // their old business unit can't possibly belong to the new company, so clear it - an admin
        // has to explicitly re-assign a business unit under the new company afterward.
        if (user.CompanyId != existing.CompanyId)
        {
            existing.CompanyId = user.CompanyId;
            existing.BusinessUnitId = null;
        }

        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            var ok = await _userRepository.UpdateAsync(existing);
            return ok ? Result.Success() : Result.Failure("Unable to update user.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update user: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(int userId, int actingUserId)
    {
        if (userId == actingUserId)
        {
            return Result.Failure("You cannot delete your own account.");
        }

        try
        {
            var ok = await _userRepository.DeleteAsync(userId);
            return ok ? Result.Success() : Result.Failure("Unable to delete user.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to delete user: {ex.Message}");
        }
    }

    public async Task<Result> ToggleActiveAsync(int userId, bool isActive)
    {
        try
        {
            var ok = await _userRepository.SetActiveAsync(userId, isActive);
            if (!isActive)
            {
                await _refreshTokenRepository.RevokeAllForUserAsync(userId);
            }
            return ok ? Result.Success() : Result.Failure("Unable to update user status.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update user status: {ex.Message}");
        }
    }

    public async Task<Result> AdminResetPasswordAsync(int userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 5)
        {
            return Result.Failure("Password must be at least 5 characters long.");
        }

        var hash = _passwordHasher.Hash(newPassword);

        try
        {
            var ok = await _userRepository.UpdatePasswordAsync(userId, hash);
            await _refreshTokenRepository.RevokeAllForUserAsync(userId);
            return ok ? Result.Success() : Result.Failure("Unable to reset password.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to reset password: {ex.Message}");
        }
    }
}
