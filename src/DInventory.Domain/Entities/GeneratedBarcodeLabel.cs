namespace DInventory.Domain.Entities;

/// <summary>A barcode + label printed ahead of formal product entry (tag first, enter into the
/// system later). Kept as a log so a printed barcode can be looked up/reused during Product entry.</summary>
public class GeneratedBarcodeLabel
{
    public int LabelId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string? SizeName { get; set; }
    public string? CompanyName { get; set; }
    public decimal? Price { get; set; }

    /// <summary>Which company this label belongs to. 0 = the built-in superuser company
    /// (bypasses company filtering everywhere); every other value is a real tenant.</summary>
    public int CompanyId { get; set; }
    public int? BusinessUnitId { get; set; }

    /// <summary>CODE128 module (bar) width in px, as printed. Default 2.</summary>
    public int BarcodeWidth { get; set; } = 2;

    /// <summary>Barcode height in px, as printed. Default 50.</summary>
    public int BarcodeHeight { get; set; } = 50;
    public bool IsLinked { get; set; }
    public int? LinkedProductVariantId { get; set; }

    /// <summary>Snapshot of the business unit name at generation time (optional - tracked for
    /// reference only, does not appear inside the Barcode string itself).</summary>
    public string? BusinessUnitName { get; set; }

    /// <summary>Optional 5-digit price code embedded in the barcode's EAN-13 body when the user
    /// supplies one (see GeneratedBarcodeLabelService). Empty when no price code was set - the
    /// barcode's item-number segment uses all available digits instead in that case.</summary>
    public string PriceCode { get; set; } = string.Empty;

    /// <summary>Legacy flag: whether a company-code segment was included as the first part of the
    /// barcode when it was generated. Barcodes no longer carry a company-code segment (it broke
    /// auto-detection on standard POS/EAN-13 scanners), so this is always false for new labels -
    /// kept only so older rows generated before this change can still be told apart in History.</summary>
    public bool IncludeCompanyCode { get; set; }

    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
}
