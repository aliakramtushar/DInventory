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

        try
        {
            var id = await _businessUnitRepository.CreateAsync(businessUnit);
            return Result<int>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save business unit: {ex.Message}");
        }
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

        try
        {
            var ok = await _businessUnitRepository.UpdateAsync(existing);
            return ok ? Result.Success() : Result.Failure("Unable to update business unit.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update business unit: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(int businessUnitId)
    {
        if (await _businessUnitRepository.HasDependentDataAsync(businessUnitId))
        {
            return Result.Failure("Cannot delete a business unit that still has users assigned to it.");
        }

        try
        {
            var ok = await _businessUnitRepository.DeleteAsync(businessUnitId);
            return ok ? Result.Success() : Result.Failure("Unable to delete business unit.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to delete business unit: {ex.Message}");
        }
    }

    public Task<(byte[] Data, string ContentType)?> GetLogoAsync(int businessUnitId) => _businessUnitRepository.GetLogoAsync(businessUnitId);

    private const int MaxLogoBytes = 50 * 1024;
    private static readonly HashSet<string> AllowedLogoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png"
    };

    public async Task<Result> UpdateLogoAsync(int businessUnitId, byte[]? logoData, string? contentType, int? actingUserId)
    {
        var existing = await _businessUnitRepository.GetByIdAsync(businessUnitId);
        if (existing is null)
        {
            return Result.Failure("Business unit not found.");
        }

        if (logoData is null || logoData.Length == 0)
        {
            // No file supplied - explicit removal.
            var cleared = await _businessUnitRepository.UpdateLogoAsync(businessUnitId, null, null, actingUserId);
            return cleared ? Result.Success() : Result.Failure("Unable to remove logo.");
        }

        if (logoData.Length > MaxLogoBytes)
        {
            return Result.Failure("Logo must be 50KB or smaller.");
        }

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedLogoContentTypes.Contains(contentType))
        {
            return Result.Failure("Logo must be a JPG or PNG image.");
        }

        if (!LooksLikeJpegOrPng(logoData))
        {
            return Result.Failure("File does not look like a valid JPG or PNG image.");
        }

        try
        {
            var normalizedContentType = contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : contentType.ToLowerInvariant();
            var ok = await _businessUnitRepository.UpdateLogoAsync(businessUnitId, logoData, normalizedContentType, actingUserId);
            return ok ? Result.Success() : Result.Failure("Unable to save logo.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to save logo: {ex.Message}");
        }
    }

    /// <summary>Defense in depth beyond the browser-supplied content type: sniffs the file's magic
    /// bytes so a renamed/mislabeled file can't slip a different format into the database.</summary>
    private static bool LooksLikeJpegOrPng(byte[] data)
    {
        if (data.Length < 8)
        {
            return false;
        }

        // JPEG: FF D8 FF ...
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return true;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        ReadOnlySpan<byte> pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        return data.AsSpan(0, 8).SequenceEqual(pngSignature);
    }
}
