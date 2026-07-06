using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Tenancy;

public class BusinessUnitService : IBusinessUnitService
{
    private readonly IBusinessUnitRepository _businessUnitRepository;

    public BusinessUnitService(IBusinessUnitRepository businessUnitRepository)
    {
        _businessUnitRepository = businessUnitRepository;
    }

    public Task<BusinessUnit?> GetByIdAsync(int businessUnitId) => _businessUnitRepository.GetByIdAsync(businessUnitId);

    public Task<IEnumerable<BusinessUnit>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false)
        => _businessUnitRepository.GetAllAsync(companyId, search, onlyActive);

    public Task<PagedResult<BusinessUnit>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false)
        => _businessUnitRepository.GetPagedAsync(request, companyId, onlyActive);

    public async Task<Result<int>> CreateAsync(BusinessUnit businessUnit, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(businessUnit.BusinessUnitName))
        {
            return Result<int>.Failure("Business unit name is required.");
        }

        if (await _businessUnitRepository.NameExistsAsync(businessUnit.CompanyId, businessUnit.BusinessUnitName))
        {
            return Result<int>.Failure("A business unit with this name already exists for this company.");
        }

        businessUnit.CreatedBy = actingUserId;
        businessUnit.CreatedAt = DateTime.UtcNow;
        businessUnit.IsActive = true;
        var id = await _businessUnitRepository.CreateAsync(businessUnit);
        return Result<int>.Success(id);
    }

    public async Task<Result> UpdateAsync(BusinessUnit businessUnit, int? actingUserId)
    {
        var existing = await _businessUnitRepository.GetByIdAsync(businessUnit.BusinessUnitId);
        if (existing is null)
        {
            return Result.Failure("Business unit not found.");
        }

        if (await _businessUnitRepository.NameExistsAsync(existing.CompanyId, businessUnit.BusinessUnitName, businessUnit.BusinessUnitId))
        {
            return Result.Failure("A business unit with this name already exists for this company.");
        }

        existing.BusinessUnitName = businessUnit.BusinessUnitName;
        existing.Address = businessUnit.Address;
        existing.IsActive = businessUnit.IsActive;
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        var ok = await _businessUnitRepository.UpdateAsync(existing);
        return ok ? Result.Success() : Result.Failure("Unable to update business unit.");
    }

    public async Task<Result> DeleteAsync(int businessUnitId)
    {
        if (await _businessUnitRepository.HasDependentDataAsync(businessUnitId))
        {
            return Result.Failure("Cannot delete a business unit that still has users assigned to it.");
        }

        var ok = await _businessUnitRepository.DeleteAsync(businessUnitId);
        return ok ? Result.Success() : Result.Failure("Unable to delete business unit.");
    }
}
