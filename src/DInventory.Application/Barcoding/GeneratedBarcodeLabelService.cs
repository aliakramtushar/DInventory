using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using System.Linq;

namespace DInventory.Application.Barcoding;

public class GeneratedBarcodeLabelService : IGeneratedBarcodeLabelService
{
    private readonly IGeneratedBarcodeLabelRepository _labelRepository;
    private readonly IBarcodeNumberGenerator _barcodeNumberGenerator;

    public GeneratedBarcodeLabelService(IGeneratedBarcodeLabelRepository labelRepository, IBarcodeNumberGenerator barcodeNumberGenerator)
    {
        _labelRepository = labelRepository;
        _barcodeNumberGenerator = barcodeNumberGenerator;
    }

    public Task<PagedResult<GeneratedBarcodeLabel>> GetPagedAsync(PagedRequest request, int companyId) => _labelRepository.GetPagedAsync(request, companyId);

    public Task<GeneratedBarcodeLabel?> GetByBarcodeAsync(string barcode) => _labelRepository.GetByBarcodeAsync(barcode.Trim());

    // Keep printed labels legible and scanner-friendly - module width of 0 (invisible bars) or a
    // multi-page-tall label are both user-input mistakes, not real print sizes, so we clamp rather
    // than reject the request outright.
    private const int MinBarcodeWidth = 1;
    private const int MaxBarcodeWidth = 6;
    private const int MinBarcodeHeight = 20;
    private const int MaxBarcodeHeight = 150;

    public async Task<Result<GeneratedBarcodeLabel>> GenerateAsync(
        int companyId,
        int? businessUnitId,
        string? businessUnitName,
        string? manualBarcode,
        string productName,
        string? brandName,
        string? sizeName,
        string? companyName,
        string? companyCode,
        string? priceCode,
        decimal? price,
        int? barcodeWidth,
        int? barcodeHeight,
        int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return Result<GeneratedBarcodeLabel>.Failure("Product name is required for the label.");
        }

        var resolvedPriceCode = string.IsNullOrWhiteSpace(priceCode) ? "000" : priceCode.Trim();

        var barcode = manualBarcode?.Trim();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            // The company code segment is mandatory for every auto-generated barcode - company name and
            // business unit are the only optional pieces on this page.
            if (string.IsNullOrWhiteSpace(companyCode))
            {
                return Result<GeneratedBarcodeLabel>.Failure("Could not resolve this company's short code, which is required for barcode generation. Set one under Companies > Edit.");
            }

            barcode = await GenerateComposedBarcodeAsync(companyId, resolvedPriceCode, companyCode);
        }
        else if (await _labelRepository.BarcodeExistsAsync(barcode))
        {
            return Result<GeneratedBarcodeLabel>.Failure($"Barcode '{barcode}' has already been generated.");
        }

        var width = Math.Clamp(barcodeWidth ?? 2, MinBarcodeWidth, MaxBarcodeWidth);
        var height = Math.Clamp(barcodeHeight ?? 50, MinBarcodeHeight, MaxBarcodeHeight);

        var label = new GeneratedBarcodeLabel
        {
            CompanyId = companyId,
            BusinessUnitId = businessUnitId,
            BusinessUnitName = string.IsNullOrWhiteSpace(businessUnitName) ? null : businessUnitName.Trim(),
            PriceCode = resolvedPriceCode,
            // The company code is now always part of the barcode - this flag exists purely for
            // historical/audit reference on older rows and is always true going forward.
            IncludeCompanyCode = true,
            Barcode = barcode,
            ProductName = productName.Trim(),
            BrandName = brandName,
            SizeName = sizeName,
            // Company name is optional - null unless the caller explicitly opted to include it (the
            // "Include company name on the label" checkbox), so it's resolved to null upstream when unchecked.
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim(),
            Price = price,
            BarcodeWidth = width,
            BarcodeHeight = height,
            IsLinked = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId
        };

        await _labelRepository.CreateAsync(label);
        return Result<GeneratedBarcodeLabel>.Success(label);
    }

    /// <summary>Builds the mandatory "CompanyCode-PriceCode-GeneratedCode" barcode. Reuses
    /// IBarcodeNumberGenerator only to obtain a fresh, collision-free numeric sequence (checked against
    /// both existing labels and live product variants) - the prefix that generator applies internally is
    /// discarded and just the digits are kept, since this format re-composes its own dash-separated
    /// prefix instead. The fully composed candidate is then re-checked for uniqueness on its own
    /// (composed strings differ from the plain "PREFIX000000123" style the raw sequence uses, so
    /// collisions here are effectively only possible if this exact composed value was already generated
    /// before), bumping the numeric tail until a free one is found.</summary>
    private async Task<string> GenerateComposedBarcodeAsync(int companyId, string priceCode, string companyCode)
    {
        var raw = await _barcodeNumberGenerator.GenerateNextAsync(companyId);
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits))
        {
            digits = "000000001";
        }

        var numberLength = digits.Length;
        var baseNumber = long.Parse(digits);

        string candidate;
        long bump = 0;
        do
        {
            var number = (baseNumber + bump).ToString().PadLeft(numberLength, '0');
            candidate = string.Join("-", new[] { companyCode.Trim().ToUpperInvariant(), priceCode, number });
            bump++;
        }
        while (await _labelRepository.BarcodeExistsAsync(candidate));

        return candidate;
    }

    public Task<bool> MarkLinkedAsync(string barcode, int productVariantId) => _labelRepository.MarkLinkedAsync(barcode.Trim(), productVariantId);
}
