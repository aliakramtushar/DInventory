using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IGeneratedBarcodeLabelRepository
{
    Task<GeneratedBarcodeLabel?> GetByBarcodeAsync(string barcode);
    /// <summary>companyId = 0 (superuser) bypasses the filter and returns labels across every company.</summary>
    Task<PagedResult<GeneratedBarcodeLabel>> GetPagedAsync(PagedRequest request, int companyId);
    Task<int> CreateAsync(GeneratedBarcodeLabel label);
    Task<bool> MarkLinkedAsync(string barcode, int productVariantId);
    Task<bool> BarcodeExistsAsync(string barcode);
    Task<string> GetLastBarcodeAsync(string prefix);
}
