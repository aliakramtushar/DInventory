using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public class ColorService : IColorService
{
    private readonly IColorRepository _colorRepository;

    public ColorService(IColorRepository colorRepository)
    {
        _colorRepository = colorRepository;
    }

    public Task<Color?> GetByIdAsync(int colorId) => _colorRepository.GetByIdAsync(colorId);

    public Task<IEnumerable<Color>> GetAllAsync(int companyId, bool onlyActive = false, int? businessUnitId = null) => _colorRepository.GetAllAsync(companyId, onlyActive, businessUnitId);

    public Task<PagedResult<Color>> GetPagedAsync(PagedRequest request, int companyId, int? businessUnitId = null) => _colorRepository.GetPagedAsync(request, companyId, businessUnitId);

    public async Task<Result<int>> CreateAsync(Color color, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(color.ColorName))
        {
            return Result<int>.Failure("Color name is required.");
        }

        if (await _colorRepository.NameExistsAsync(color.CompanyId, color.ColorName))
        {
            return Result<int>.Failure("This color already exists.");
        }

        color.CreatedAt = DateTime.UtcNow;
        color.IsActive = true;

        try
        {
            var id = await _colorRepository.CreateAsync(color);
            return Result<int>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save color: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(Color color, int? actingUserId)
    {
        var existing = await _colorRepository.GetByIdAsync(color.ColorId);
        if (existing is null)
        {
            return Result.Failure("Color not found.");
        }

        if (await _colorRepository.NameExistsAsync(existing.CompanyId, color.ColorName, color.ColorId))
        {
            return Result.Failure("This color already exists.");
        }

        existing.ColorName = color.ColorName;
        existing.HexCode = color.HexCode;
        existing.DisplayOrder = color.DisplayOrder;
        existing.IsActive = color.IsActive;

        try
        {
            var ok = await _colorRepository.UpdateAsync(existing);
            return ok ? Result.Success() : Result.Failure("Unable to update color.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update color: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(int colorId)
    {
        if (await _colorRepository.HasVariantsAsync(colorId))
        {
            return Result.Failure("Cannot delete a color that still has product variants assigned to it.");
        }

        try
        {
            var ok = await _colorRepository.DeleteAsync(colorId);
            return ok ? Result.Success() : Result.Failure("Unable to delete color.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to delete color: {ex.Message}");
        }
    }
}
