using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public class SubcategoryService : ISubcategoryService
{
    private readonly ISubcategoryRepository _subcategoryRepository;

    public SubcategoryService(ISubcategoryRepository subcategoryRepository)
    {
        _subcategoryRepository = subcategoryRepository;
    }

    public Task<Subcategory?> GetByIdAsync(int subcategoryId) => _subcategoryRepository.GetByIdAsync(subcategoryId);

    public Task<IEnumerable<Subcategory>> GetAllAsync(int companyId, int? categoryId = null, string? search = null, bool onlyActive = false)
        => _subcategoryRepository.GetAllAsync(companyId, categoryId, search, onlyActive);

    public Task<PagedResult<Subcategory>> GetPagedAsync(PagedRequest request, int companyId, int? categoryId = null, bool onlyActive = false)
        => _subcategoryRepository.GetPagedAsync(request, companyId, categoryId, onlyActive);

    public async Task<Result<int>> CreateAsync(Subcategory subcategory, int? actingUserId)
    {
        subcategory.CreatedBy = actingUserId;
        subcategory.CreatedAt = DateTime.UtcNow;
        subcategory.IsActive = true;

        try
        {
            var id = await _subcategoryRepository.CreateAsync(subcategory);
            return Result<int>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save subcategory: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(Subcategory subcategory, int? actingUserId)
    {
        var existing = await _subcategoryRepository.GetByIdAsync(subcategory.SubcategoryId);
        if (existing is null)
        {
            return Result.Failure("Subcategory not found.");
        }

        existing.CategoryId = subcategory.CategoryId;
        existing.SubcategoryName = subcategory.SubcategoryName;
        existing.Description = subcategory.Description;
        existing.IsActive = subcategory.IsActive;
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            var ok = await _subcategoryRepository.UpdateAsync(existing);
            return ok ? Result.Success() : Result.Failure("Unable to update subcategory.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update subcategory: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(int subcategoryId)
    {
        if (await _subcategoryRepository.HasProductsAsync(subcategoryId))
        {
            return Result.Failure("Cannot delete a subcategory that still has products assigned to it.");
        }

        try
        {
            var ok = await _subcategoryRepository.DeleteAsync(subcategoryId);
            return ok ? Result.Success() : Result.Failure("Unable to delete subcategory.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to delete subcategory: {ex.Message}");
        }
    }
}
