namespace DInventory.Application.Barcoding;

/// <summary>
/// Generates the next unique barcode value in DInventory's own numbering series (used when the user
/// doesn't scan/type an existing barcode). Keeping this behind one interface means the barcode
/// generator page and product-variant entry never hand out the same number twice.
/// </summary>
public interface IBarcodeNumberGenerator
{
    /// <summary>companyId selects which tenant's ShortName is used as the barcode prefix (falls
    /// back to "DIN" if the company can't be resolved or has no short name yet).</summary>
    Task<string> GenerateNextAsync(int companyId);
}
