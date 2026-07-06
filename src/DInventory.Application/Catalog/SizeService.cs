using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public class SizeService : ISizeService
{
    private readonly ISizeRepository _sizeRepository;

    public SizeService(ISizeRepository sizeRepository)
    {
        _sizeRepository = sizeRepository;
    }

    public Task<Size?> GetByIdAsync(int sizeId) => _sizeRepository.GetByIdAsync(sizeId);

    public Task<IEnumerable<Size>> GetAllAsync(int companyId, bool onlyActive = false) => _sizeRepository.GetAllAsync(companyId, onlyActive);

    public Task<PagedResult<Size>> GetPagedAsync(PagedRequest request, int companyId) => _sizeRepository.GetPagedAsync(request, companyId);

    public async Task<Result<int>> CreateAsync(Size size, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(size.SizeName))
        {
            return Result<int>.Failure("Size name is required.");
        }

        if (await _sizeRepository.NameExistsAsync(size.CompanyId, size.SizeName))
        {
            return Result<int>.Failure("This size already exists.");
        }

        size.CreatedAt = DateTime.UtcNow;
        size.IsActive = true;
        var id = await _sizeRepository.CreateAsync(size);
        return Result<int>.Success(id);
    }

    public async Task<Result> UpdateAsync(Size size, int? actingUserId)
    {
        var existing = await _sizeRepository.GetByIdAsync(size.SizeId);
        if (existing is null)
        {
            return Result.Failure("Size not found.");
        }

        if (await _sizeRepository.NameExistsAsync(existing.CompanyId, size.SizeName, size.SizeId))
        {
            return Result.Failure("This size already exists.");
        }

        existing.SizeName = size.SizeName;
        existing.DisplayOrder = size.DisplayOrder;
        existing.IsActive = size.IsActive;

        var ok = await _sizeRepository.UpdateAsync(existing);
        return ok ? Result.Success() : Result.Failure("Unable to update size.");
    }

    public async Task<Result> DeleteAsync(int sizeId)
    {
        if (await _sizeRepository.HasVariantsAsync(sizeId))
        {
            return Result.Failure("Cannot delete a size that still has product variants assigned to it.");
        }

        var ok = await _sizeRepository.DeleteAsync(sizeId);
        return ok ? Result.Success() : Result.Failure("Unable to delete size.");
    }
}
