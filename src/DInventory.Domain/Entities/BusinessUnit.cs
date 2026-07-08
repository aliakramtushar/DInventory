namespace DInventory.Domain.Entities;

/// <summary>An optional sub-division within a Company (e.g. a branch/outlet). Nullable everywhere
/// it's referenced - a company that doesn't need business units can just leave it blank, same as
/// ProductVariants.ColorId being optional.</summary>
public class BusinessUnit
{
    public int BusinessUnitId { get; set; }
    public int CompanyId { get; set; }
    public string BusinessUnitName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    /// <summary>Raw logo bytes - only populated when explicitly fetched via
    /// IBusinessUnitRepository.GetLogoAsync (see BusinessUnitLogoController). Left null everywhere
    /// else (list/edit-form queries) so ordinary reads never drag a blob along for the ride.</summary>
    public byte[]? Logo { get; set; }

    /// <summary>MIME type of <see cref="Logo"/> (image/jpeg or image/png) - only meaningful when
    /// Logo is populated.</summary>
    public string? LogoContentType { get; set; }

    // Populated via join, not a DB column
    public string? CompanyName { get; set; }

    /// <summary>Computed (CASE WHEN Logo IS NOT NULL...), not a DB column - lets list/edit views know
    /// whether a logo exists without loading its bytes.</summary>
    public bool HasLogo { get; set; }
}
