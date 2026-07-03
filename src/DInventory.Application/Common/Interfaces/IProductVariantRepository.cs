using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IProductVariantRepository
{
    Task<ProductVariant?> GetByIdAsync(int productVariantId);
    Task<ProductVariant?> GetByBarcodeAsync(string barcode);
    Task<IEnumerable<ProductVariant>> GetByProductIdAsync(int productId);
    Task<PagedResult<ProductVariant>> GetPagedAsync(PagedRequest request, int? categoryId = null, int? brandId = null, int? colorId = null, bool onlyActive = false);
    Task<int> CreateAsync(ProductVariant variant);
    Task<bool> UpdateAsync(ProductVariant variant);
    Task<bool> DeleteAsync(int productVariantId);
    Task<bool> BarcodeExistsAsync(string barcode, int? excludeId = null);
}
