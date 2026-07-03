namespace DInventory.Application.Barcoding;

/// <summary>
/// Generates the next unique barcode value in DInventory's own numbering series (used when the user
/// doesn't scan/type an existing barcode). Keeping this behind one interface means the barcode
/// generator page and product-variant entry never hand out the same number twice.
/// </summary>
public interface IBarcodeNumberGenerator
{
    Task<string> GenerateNextAsync();
}
