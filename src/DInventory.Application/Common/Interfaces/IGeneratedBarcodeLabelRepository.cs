using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IGeneratedBarcodeLabelRepository
{
    Task<GeneratedBarcodeLabel?> GetByBarcodeAsync(string barcode);
    Task<PagedResult<GeneratedBarcodeLabel>> GetPagedAsync(PagedRequest request);
    Task<int> CreateAsync(GeneratedBarcodeLabel label);
    Task<bool> MarkLinkedAsync(string barcode, int productVariantId);
    Task<bool> BarcodeExistsAsync(string barcode);
    Task<string> GetLastBarcodeAsync();
}
