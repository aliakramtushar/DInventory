using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public Task<Category?> GetByIdAsync(int categoryId) => _categoryRepository.GetByIdAsync(categoryId);

    public Task<IEnumerable<Category>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false)
        => _categoryRepository.GetAllAsync(companyId, search, onlyActive);

    public Task<PagedResult<Category>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false)
        => _categoryRepository.GetPagedAsync(request, companyId, onlyActive);

    public async Task<Result<int>> CreateAsync(Category category, int? actingUserId)
    {
        if (await _categoryRepository.NameExistsAsync(category.CompanyId, category.CategoryName))
        {
            return Result<int>.Failure("A category with this name already exists.");
        }

        category.CreatedBy = actingUserId;
        category.CreatedAt = DateTime.UtcNow;
        category.IsActive = true;

        try
        {
            var id = await _categoryRepository.CreateAsync(category);
            return Result<int>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save category: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(Category category, int? actingUserId)
    {
        var existing = await _categoryRepository.GetByIdAsync(category.CategoryId);
        if (existing is null)
        {
            return Result.Failure("Category not found.");
        }

        if (await _categoryRepository.NameExistsAsync(existing.CompanyId, category.CategoryName, category.CategoryId))
        {
            return Result.Failure("A category with this name already exists.");
        }

        existing.CategoryName = category.CategoryName;
        existing.Description = category.Description;
        existing.IsActive = category.IsActive;
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            var ok = await _categoryRepository.UpdateAsync(existing);
            return ok ? Result.Success() : Result.Failure("Unable to update category.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update category: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(int categoryId)
    {
        if (await _categoryRepository.HasProductsAsync(categoryId))
        {
            return Result.Failure("Cannot delete a category that still has products assigned to it.");
        }

        try
        {
            var ok = await _categoryRepository.DeleteAsync(categoryId);
            return ok ? Result.Success() : Result.Failure("Unable to delete category.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to delete category: {ex.Message}");
        }
    }
}
