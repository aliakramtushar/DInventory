using DInventory.Application.Common.Interfaces;

namespace DInventory.Application.Barcoding;

/// <summary>
/// Sequential "DIN" + 9-digit series (e.g. DIN000000123), checked against both the printed-label log
/// and live product variants so a freshly generated number can never collide with one already in use.
/// </summary>
public class BarcodeNumberGenerator : IBarcodeNumberGenerator
{
    /// <summary>Used only if a company can't be resolved or hasn't set a ShortName yet - every real
    /// company should have its own prefix via Companies.ShortName instead.</summary>
    private const string FallbackPrefix = "DIN";
    private readonly IGeneratedBarcodeLabelRepository _labelRepository;
    private readonly IProductVariantRepository _variantRepository;
    private readonly ICompanyRepository _companyRepository;

    public BarcodeNumberGenerator(
        IGeneratedBarcodeLabelRepository labelRepository,
        IProductVariantRepository variantRepository,
        ICompanyRepository companyRepository)
    {
        _labelRepository = labelRepository;
        _variantRepository = variantRepository;
        _companyRepository = companyRepository;
    }

    public async Task<string> GenerateNextAsync(int companyId)
    {
        var company = await _companyRepository.GetByIdAsync(companyId);
        var prefix = !string.IsNullOrWhiteSpace(company?.ShortName) ? company.ShortName : FallbackPrefix;

        var last = await _labelRepository.GetLastBarcodeAsync(prefix);
        var next = 1;
        if (!string.IsNullOrWhiteSpace(last) && last.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(last[prefix.Length..], out var lastNumber))
        {
            next = lastNumber + 1;
        }

        string candidate;
        do
        {
            candidate = $"{prefix}{next:D9}";
            next++;
        }
        while (await _labelRepository.BarcodeExistsAsync(candidate) || await _variantRepository.BarcodeExistsAsync(candidate));

        return candidate;
    }
}
