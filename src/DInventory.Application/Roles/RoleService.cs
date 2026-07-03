using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Roles;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;

    public RoleService(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public Task<Role?> GetByIdAsync(int roleId) => _roleRepository.GetByIdAsync(roleId);

    public Task<IEnumerable<Role>> GetAllAsync() => _roleRepository.GetAllAsync();

    public Task<PagedResult<Role>> GetPagedAsync(PagedRequest request) => _roleRepository.GetPagedAsync(request);

    public async Task<Result<int>> CreateAsync(Role role)
    {
        var existing = await _roleRepository.GetByNameAsync(role.RoleName);
        if (existing is not null)
        {
            return Result<int>.Failure("A role with this name already exists.");
        }

        role.IsSystemRole = false;
        role.CreatedAt = DateTime.UtcNow;
        var id = await _roleRepository.CreateAsync(role);
        return Result<int>.Success(id);
    }

    public async Task<Result> UpdateAsync(Role role)
    {
        var existing = await _roleRepository.GetByIdAsync(role.RoleId);
        if (existing is null)
        {
            return Result.Failure("Role not found.");
        }

        if (existing.IsSystemRole && !existing.RoleName.Equals(role.RoleName, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("System roles (SuperAdmin / Admin) cannot be renamed.");
        }

        existing.RoleName = role.RoleName;
        existing.Description = role.Description;
        existing.IsActive = existing.IsSystemRole || role.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        var ok = await _roleRepository.UpdateAsync(existing);
        return ok ? Result.Success() : Result.Failure("Unable to update role.");
    }

    public async Task<Result> DeleteAsync(int roleId)
    {
        var existing = await _roleRepository.GetByIdAsync(roleId);
        if (existing is null)
        {
            return Result.Failure("Role not found.");
        }

        if (existing.IsSystemRole)
        {
            return Result.Failure("System roles (SuperAdmin / Admin) cannot be deleted.");
        }

        var userCount = await _roleRepository.GetUserCountForRoleAsync(roleId);
        if (userCount > 0)
        {
            return Result.Failure($"Cannot delete role: {userCount} user(s) are still assigned to it.");
        }

        var ok = await _roleRepository.DeleteAsync(roleId);
        return ok ? Result.Success() : Result.Failure("Unable to delete role.");
    }
}
