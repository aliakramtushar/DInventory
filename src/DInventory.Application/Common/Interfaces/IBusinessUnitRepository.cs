using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IBusinessUnitRepository
{
    Task<BusinessUnit?> GetByIdAsync(int businessUnitId);
    Task<IEnumerable<BusinessUnit>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false);
    Task<PagedResult<BusinessUnit>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false);
    Task<int> CreateAsync(BusinessUnit businessUnit);
    Task<bool> UpdateAsync(BusinessUnit businessUnit);
    Task<bool> DeleteAsync(int businessUnitId);
    Task<bool> HasDependentDataAsync(int businessUnitId);
    Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null);

    /// <summary>Fetches just the logo bytes + content type for serving as an image response - kept
    /// separate from GetByIdAsync/GetAllAsync/GetPagedAsync so ordinary list/edit queries never load
    /// the blob. Null if the business unit doesn't exist or has no logo.</summary>
    Task<(byte[] Data, string ContentType)?> GetLogoAsync(int businessUnitId);

    /// <summary>Sets (logo/contentType both non-null) or clears (both null) the stored logo.</summary>
    Task<bool> UpdateLogoAsync(int businessUnitId, byte[]? logo, string? contentType, int? actingUserId);
}
