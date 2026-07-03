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

    public Task<IEnumerable<Color>> GetAllAsync(bool onlyActive = false) => _colorRepository.GetAllAsync(onlyActive);

    public Task<PagedResult<Color>> GetPagedAsync(PagedRequest request) => _colorRepository.GetPagedAsync(request);

    public async Task<Result<int>> CreateAsync(Color color, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(color.ColorName))
        {
            return Result<int>.Failure("Color name is required.");
        }

        if (await _colorRepository.NameExistsAsync(color.ColorName))
        {
            return Result<int>.Failure("This color already exists.");
        }

        color.CreatedAt = DateTime.UtcNow;
        color.IsActive = true;
        var id = await _colorRepository.CreateAsync(color);
        return Result<int>.Success(id);
    }

    public async Task<Result> UpdateAsync(Color color, int? actingUserId)
    {
        var existing = await _colorRepository.GetByIdAsync(color.ColorId);
        if (existing is null)
        {
            return Result.Failure("Color not found.");
        }

        if (await _colorRepository.NameExistsAsync(color.ColorName, color.ColorId))
        {
            return Result.Failure("This color already exists.");
        }

        existing.ColorName = color.ColorName;
        existing.HexCode = color.HexCode;
        existing.DisplayOrder = color.DisplayOrder;
        existing.IsActive = color.IsActive;

        var ok = await _colorRepository.UpdateAsync(existing);
        return ok ? Result.Success() : Result.Failure("Unable to update color.");
    }

    public async Task<Result> DeleteAsync(int colorId)
    {
        if (await _colorRepository.HasVariantsAsync(colorId))
        {
            return Result.Failure("Cannot delete a color that still has product variants assigned to it.");
        }

        var ok = await _colorRepository.DeleteAsync(colorId);
        return ok ? Result.Success() : Result.Failure("Unable to delete color.");
    }
}
