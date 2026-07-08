using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using System.Linq;

namespace DInventory.Application.Barcoding;

public class GeneratedBarcodeLabelService : IGeneratedBarcodeLabelService
{
    private readonly IGeneratedBarcodeLabelRepository _labelRepository;
    private readonly IProductVariantRepository _variantRepository;
    private readonly IBarcodeNumberGenerator _barcodeNumberGenerator;

    public GeneratedBarcodeLabelService(
        IGeneratedBarcodeLabelRepository labelRepository,
        IProductVariantRepository variantRepository,
        IBarcodeNumberGenerator barcodeNumberGenerator)
    {
        _labelRepository = labelRepository;
        _variantRepository = variantRepository;
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

    // EAN-13 is a fixed 13 digits, so the price-code segment (when one is supplied) and the plain
    // item-number segment (when one isn't) both have to be sized to fit the same 10-digit body that
    // follows the 2-digit internal-use prefix (see Ean13.InternalUsePrefix):
    //   no price code:   "20" + 10-digit item number                      + check digit = 13 digits
    //   with price code: "20" + 5-digit item number + 5-digit price code  + check digit = 13 digits
    private const int PriceCodeDigitCount = 5;
    private const int ItemNumberDigitCountWithPriceCode = 5;
    private const int ItemNumberDigitCountNoPriceCode = 10;

    public async Task<Result<GeneratedBarcodeLabel>> GenerateAsync(
        int companyId,
        int? businessUnitId,
        string? businessUnitName,
        string? manualBarcode,
        string productName,
        string? brandName,
        string? sizeName,
        string? companyName,
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

        // A price code is genuinely optional now: when supplied it's embedded as its own 5-digit
        // segment of the barcode; when it isn't, the barcode's item-number segment simply uses all
        // 10 available digits instead of padding in a meaningless placeholder. Non-digit characters
        // are stripped because a POS/EAN-13 scanner only ever reads numerals - this keeps the final
        // barcode globally scannable no matter what the user typed into the Price Code field.
        var hasPriceCode = !string.IsNullOrWhiteSpace(priceCode);
        var resolvedPriceCode = hasPriceCode ? Ean13.NormalizeDigits(priceCode, PriceCodeDigitCount) : null;

        var barcode = manualBarcode?.Trim();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            barcode = await GenerateEan13BarcodeAsync(resolvedPriceCode);
        }
        else
        {
            // Accept a real supplier-printed UPC-A/EAN-13 barcode too (scanned or typed in) -
            // normalizes a 12-digit UPC-A to its equivalent 13-digit EAN-13 form.
            var normalized = Ean13.NormalizeManualBarcode(barcode);
            if (normalized is null)
            {
                return Result<GeneratedBarcodeLabel>.Failure(
                    $"'{barcode}' is not a valid EAN-13/UPC-A barcode (13 digits, or 12 for UPC-A, with a correct check digit). " +
                    "Scan/type an existing barcode, or leave the field blank to auto-generate one.");
            }

            barcode = normalized;
            if (await _labelRepository.BarcodeExistsAsync(barcode))
            {
                return Result<GeneratedBarcodeLabel>.Failure($"Barcode '{barcode}' has already been generated.");
            }
        }

        var width = Math.Clamp(barcodeWidth ?? 2, MinBarcodeWidth, MaxBarcodeWidth);
        var height = Math.Clamp(barcodeHeight ?? 50, MinBarcodeHeight, MaxBarcodeHeight);

        try
        {
            var label = new GeneratedBarcodeLabel
            {
                CompanyId = companyId,
                BusinessUnitId = businessUnitId,
                BusinessUnitName = string.IsNullOrWhiteSpace(businessUnitName) ? null : businessUnitName.Trim(),
                PriceCode = resolvedPriceCode ?? string.Empty,
                // The barcode text no longer carries a company-code segment - kept false going
                // forward so it's clear (here and in History) which labels predate this change.
                IncludeCompanyCode = false,
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
        catch (Exception ex)
        {
            return Result<GeneratedBarcodeLabel>.Failure($"Unable to save barcode label: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds a fresh, checksummed EAN-13 barcode: GS1's "20" internal-use prefix, then either a
    /// plain 10-digit sequential item number (no price code - IBarcodeNumberGenerator's result is
    /// already a complete, unique barcode in this shape, so it's returned as-is), or a 5-digit item
    /// number followed by the 5-digit price code when one was supplied. In the price-code case the
    /// body differs from what IBarcodeNumberGenerator checked, so the composed candidate is
    /// re-checked for uniqueness against both existing labels and live product variants here,
    /// bumping the item number until a free one is found.
    /// </summary>
    private async Task<string> GenerateEan13BarcodeAsync(string? priceCode)
    {
        // Already a complete, checksummed, and unique (against both labels and live variants)
        // EAN-13 barcode - use it as-is when no price code needs to be embedded.
        var seedBarcode = await _barcodeNumberGenerator.GenerateNextAsync();
        if (priceCode is null)
        {
            return seedBarcode;
        }

        // Embedding a price code replaces part of the item-number segment, which changes the body
        // and therefore the check digit - re-derive a numeric starting point from that same seed and
        // re-check uniqueness against both existing labels and live product variants, bumping
        // further if needed.
        var seedNumber = long.Parse(seedBarcode.Substring(Ean13.InternalUsePrefix.Length, ItemNumberDigitCountNoPriceCode));
        var itemModulus = (long)Math.Pow(10, ItemNumberDigitCountWithPriceCode);

        string candidate;
        long bump = 0;
        do
        {
            var itemNumber = ((seedNumber + bump) % itemModulus).ToString().PadLeft(ItemNumberDigitCountWithPriceCode, '0');
            var body = Ean13.InternalUsePrefix + itemNumber + priceCode;
            candidate = Ean13.Compose(body);
            bump++;
        }
        while (await _labelRepository.BarcodeExistsAsync(candidate) || await _variantRepository.BarcodeExistsAsync(candidate));

        return candidate;
    }

    public Task<bool> MarkLinkedAsync(string barcode, int productVariantId) => _labelRepository.MarkLinkedAsync(barcode.Trim(), productVariantId);
}
