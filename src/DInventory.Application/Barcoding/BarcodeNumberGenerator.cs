using DInventory.Application.Common.Interfaces;

namespace DInventory.Application.Barcoding;

/// <summary>
/// Sequential "20" + 10-digit series (e.g. 2000000001238) - the complete, ready-to-use EAN-13
/// barcode this app hands out directly wherever a product/variant needs an auto-assigned barcode
/// (ProductService/ProductVariantService), and the numeric base GeneratedBarcodeLabelService
/// re-derives from when a price code needs to be embedded instead. Checked against both the
/// printed-label log and live product variants so a freshly generated barcode can never collide
/// with one already in use.
/// </summary>
public class BarcodeNumberGenerator : IBarcodeNumberGenerator
{
    private const int ItemDigitCount = 10;

    private readonly IGeneratedBarcodeLabelRepository _labelRepository;
    private readonly IProductVariantRepository _variantRepository;

    public BarcodeNumberGenerator(
        IGeneratedBarcodeLabelRepository labelRepository,
        IProductVariantRepository variantRepository)
    {
        _labelRepository = labelRepository;
        _variantRepository = variantRepository;
    }

    public async Task<string> GenerateNextAsync()
    {
        var last = await _labelRepository.GetLastBarcodeAsync(Ean13.InternalUsePrefix);

        var next = 1L;
        if (!string.IsNullOrWhiteSpace(last)
            && last.Length >= Ean13.InternalUsePrefix.Length + ItemDigitCount
            && long.TryParse(last.AsSpan(Ean13.InternalUsePrefix.Length, ItemDigitCount), out var lastNumber))
        {
            next = lastNumber + 1;
        }

        string candidate;
        do
        {
            var itemNumber = next.ToString().PadLeft(ItemDigitCount, '0');
            candidate = Ean13.Compose(Ean13.InternalUsePrefix + itemNumber);
            next++;
        }
        while (await _labelRepository.BarcodeExistsAsync(candidate) || await _variantRepository.BarcodeExistsAsync(candidate));

        return candidate;
    }
}
