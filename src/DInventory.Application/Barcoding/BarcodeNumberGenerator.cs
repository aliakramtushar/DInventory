using DInventory.Application.Common.Interfaces;

namespace DInventory.Application.Barcoding;

/// <summary>
/// Sequential "DIN" + 9-digit series (e.g. DIN000000123), checked against both the printed-label log
/// and live product variants so a freshly generated number can never collide with one already in use.
/// </summary>
public class BarcodeNumberGenerator : IBarcodeNumberGenerator
{
    private const string Prefix = "DIN";
    private readonly IGeneratedBarcodeLabelRepository _labelRepository;
    private readonly IProductVariantRepository _variantRepository;

    public BarcodeNumberGenerator(IGeneratedBarcodeLabelRepository labelRepository, IProductVariantRepository variantRepository)
    {
        _labelRepository = labelRepository;
        _variantRepository = variantRepository;
    }

    public async Task<string> GenerateNextAsync()
    {
        var last = await _labelRepository.GetLastBarcodeAsync();
        var next = 1;
        if (!string.IsNullOrWhiteSpace(last) && last.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(last[Prefix.Length..], out var lastNumber))
        {
            next = lastNumber + 1;
        }

        string candidate;
        do
        {
            candidate = $"{Prefix}{next:D9}";
            next++;
        }
        while (await _labelRepository.BarcodeExistsAsync(candidate) || await _variantRepository.BarcodeExistsAsync(candidate));

        return candidate;
    }
}
