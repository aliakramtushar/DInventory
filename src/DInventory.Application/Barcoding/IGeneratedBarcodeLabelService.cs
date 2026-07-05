using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Barcoding;

public interface IGeneratedBarcodeLabelService
{
    Task<PagedResult<GeneratedBarcodeLabel>> GetPagedAsync(PagedRequest request);
    Task<GeneratedBarcodeLabel?> GetByBarcodeAsync(string barcode);

    /// <summary>
    /// Print-tag-first workflow: generate a brand-new barcode (or accept a manually supplied one) plus
    /// product name/brand/size/price, so it can be printed and physically stuck on stock before that
    /// stock is formally entered into the system as a product variant.
    /// </summary>
    Task<Result<GeneratedBarcodeLabel>> GenerateAsync(string? manualBarcode, string productName, string? brandName, string? sizeName, string? companyName, decimal? price, int? barcodeWidth, int? barcodeHeight, int? actingUserId);
}
