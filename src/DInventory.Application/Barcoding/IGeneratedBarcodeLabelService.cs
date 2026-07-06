using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Barcoding;

public interface IGeneratedBarcodeLabelService
{
    Task<PagedResult<GeneratedBarcodeLabel>> GetPagedAsync(PagedRequest request, int companyId);
    Task<GeneratedBarcodeLabel?> GetByBarcodeAsync(string barcode);

    /// <summary>
    /// Print-tag-first workflow: generate a brand-new barcode (or accept a manually supplied one) plus
    /// product name/brand/size/price, so it can be printed and physically stuck on stock before that
    /// stock is formally entered into the system as a product variant.
    /// </summary>
    Task<Result<GeneratedBarcodeLabel>> GenerateAsync(
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
        int? actingUserId);

    /// <summary>Marks a previously generated label as linked once its barcode is actually used to
    /// create/assign a real product variant, so the History page's Linked/Unlinked badge reflects
    /// reality instead of always showing Unlinked. No-op (returns false) if no label with that
    /// barcode exists - most variant barcodes are never printed via the generator first.</summary>
    Task<bool> MarkLinkedAsync(string barcode, int productVariantId);
}
