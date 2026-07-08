using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Tenancy;

public interface IBusinessUnitService
{
    Task<BusinessUnit?> GetByIdAsync(int businessUnitId);
    Task<IEnumerable<BusinessUnit>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false);
    Task<PagedResult<BusinessUnit>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false);
    Task<Result<int>> CreateAsync(BusinessUnit businessUnit, int? actingUserId);
    Task<Result> UpdateAsync(BusinessUnit businessUnit, int? actingUserId);
    Task<Result> DeleteAsync(int businessUnitId);

    /// <summary>Fetches the raw logo bytes + content type for serving as an image response. Null if
    /// the business unit doesn't exist or has no logo.</summary>
    Task<(byte[] Data, string ContentType)?> GetLogoAsync(int businessUnitId);

    /// <summary>Validates (jpg/jpeg/png only, 50KB max) and stores a new logo, or clears it when
    /// <paramref name="logoData"/> is null.</summary>
    Task<Result> UpdateLogoAsync(int businessUnitId, byte[]? logoData, string? contentType, int? actingUserId);
}
