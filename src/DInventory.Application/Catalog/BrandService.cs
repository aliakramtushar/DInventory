using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public class BrandService : IBrandService
{
    private readonly IBrandRepository _brandRepository;

    public BrandService(IBrandRepository brandRepository)
    {
        _brandRepository = brandRepository;
    }

    public Task<Brand?> GetByIdAsync(int brandId) => _brandRepository.GetByIdAsync(brandId);

    public Task<IEnumerable<Brand>> GetAllAsync(int companyId, bool onlyActive = false) => _brandRepository.GetAllAsync(companyId, onlyActive);

    public Task<PagedResult<Brand>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false)
        => _brandRepository.GetPagedAsync(request, companyId, onlyActive);

    public async Task<Result<int>> CreateAsync(Brand brand, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(brand.BrandName))
        {
            return Result<int>.Failure("Brand name is required.");
        }

        if (await _brandRepository.NameExistsAsync(brand.CompanyId, brand.BrandName))
        {
            return Result<int>.Failure("A brand with this name already exists.");
        }

        brand.CreatedBy = actingUserId;
        brand.CreatedAt = DateTime.UtcNow;
        brand.IsActive = true;
        var id = await _brandRepository.CreateAsync(brand);
        return Result<int>.Success(id);
    }

    public async Task<Result> UpdateAsync(Brand brand, int? actingUserId)
    {
        var existing = await _brandRepository.GetByIdAsync(brand.BrandId);
        if (existing is null)
        {
            return Result.Failure("Brand not found.");
        }

        if (await _brandRepository.NameExistsAsync(existing.CompanyId, brand.BrandName, brand.BrandId))
        {
            return Result.Failure("A brand with this name already exists.");
        }

        existing.BrandName = brand.BrandName;
        existing.Description = brand.Description;
        existing.IsActive = brand.IsActive;
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        var ok = await _brandRepository.UpdateAsync(existing);
        return ok ? Result.Success() : Result.Failure("Unable to update brand.");
    }

    public async Task<Result> DeleteAsync(int brandId)
    {
        if (await _brandRepository.HasProductsAsync(brandId))
        {
            return Result.Failure("Cannot delete a brand that still has products assigned to it.");
        }

        var ok = await _brandRepository.DeleteAsync(brandId);
        return ok ? Result.Success() : Result.Failure("Unable to delete brand.");
    }
}
