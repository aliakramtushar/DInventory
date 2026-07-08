using System.Linq;

namespace DInventory.Application.Barcoding;

/// <summary>
/// Minimal helpers for building and validating EAN-13 barcodes - the barcode standard used at
/// retail POS counters worldwide (UPC-A, the North American standard, is just an EAN-13 with a
/// leading "0", so validating/accepting 13 numeric digits here covers both). Every barcode this
/// app hands out is exactly 13 digits with a correct check digit so it can be scanned by any
/// standard POS/handheld scanner without extra configuration - no company-specific prefix or
/// symbology setup required on the scanner's end.
/// </summary>
public static class Ean13
{
    /// <summary>
    /// GS1's "restricted circulation number" prefix range (20-29) is reserved worldwide for
    /// internal/in-store use - i.e. numbers a business can assign itself without a licensed GS1
    /// company prefix, guaranteed to never collide with a real retail product's GTIN. This is the
    /// standard, safe choice for a self-hosted inventory system that generates its own barcodes.
    /// </summary>
    public const string InternalUsePrefix = "20";

    /// <summary>Computes the standard EAN-13 (Mod10) check digit for a 12-digit numeric payload.</summary>
    public static char ComputeCheckDigit(string twelveDigits)
    {
        if (twelveDigits is null || twelveDigits.Length != 12 || !twelveDigits.All(char.IsDigit))
        {
            throw new ArgumentException("EAN-13 payload must be exactly 12 digits.", nameof(twelveDigits));
        }

        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var digit = twelveDigits[i] - '0';
            sum += digit * (i % 2 == 0 ? 1 : 3);
        }

        var checkDigit = (10 - (sum % 10)) % 10;
        return (char)('0' + checkDigit);
    }

    /// <summary>Builds the full 13-digit barcode from a 12-digit payload by appending its check digit.</summary>
    public static string Compose(string twelveDigits) => twelveDigits + ComputeCheckDigit(twelveDigits);

    /// <summary>True if the value is exactly 13 digits and its check digit is correct - i.e. it's a
    /// well-formed, globally scannable EAN-13 barcode.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 13 || !value.All(char.IsDigit))
        {
            return false;
        }

        return ComputeCheckDigit(value[..12]) == value[12];
    }

    /// <summary>
    /// Normalizes a manually typed/scanned barcode so a real supplier-issued UPC-A code (12 digits)
    /// is accepted the same way a scanner or till would treat it - by left-padding it with a "0" to
    /// become an EAN-13, since UPC-A is defined as a subset of EAN-13. Returns null if the value
    /// isn't a valid 12-digit UPC-A or 13-digit EAN-13 barcode.
    /// </summary>
    public static string? NormalizeManualBarcode(string? raw)
    {
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.All(char.IsDigit))
        {
            return null;
        }

        if (trimmed.Length == 13 && IsValid(trimmed))
        {
            return trimmed;
        }

        if (trimmed.Length == 12)
        {
            var asEan13 = "0" + trimmed;
            return IsValid(asEan13) ? asEan13 : null;
        }

        return null;
    }

    /// <summary>Keeps digits only, then keeps just the trailing <paramref name="length"/> digits and
    /// zero-pads on the left as needed - used to fit a free-form price code into its fixed-width
    /// barcode segment.</summary>
    public static string NormalizeDigits(string? value, int length)
    {
        var digits = value is null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length > length)
        {
            digits = digits[^length..];
        }

        return digits.PadLeft(length, '0');
    }
}
