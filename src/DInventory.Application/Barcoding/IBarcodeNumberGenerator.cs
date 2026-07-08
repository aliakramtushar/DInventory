namespace DInventory.Application.Barcoding;

/// <summary>
/// Hands out a fresh, ready-to-use EAN-13 barcode - GS1's "20" restricted-circulation/internal-use
/// prefix, a sequential 10-digit item number, and the standard EAN-13 check digit (13 digits total,
/// numeric only). There's no company-code prefix: the barcode has to stay scannable by any standard
/// POS/EAN-13 scanner regardless of which company/tenant it belongs to, so this is a single global
/// series rather than one series per company. The result is already checked unique against both
/// previously generated labels and live product variants, so callers can use it as-is.
/// </summary>
public interface IBarcodeNumberGenerator
{
    Task<string> GenerateNextAsync();
}
