using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public interface IProductVariantService
{
    Task<ProductVariant?> GetByIdAsync(int productVariantId);
    Task<ProductVariant?> GetByBarcodeAsync(string barcode);
    Task<IEnumerable<ProductVariant>> GetByProductIdAsync(int productId);
    Task<PagedResult<ProductVariant>> GetPagedAsync(PagedRequest request, int companyId = 0, int? categoryId = null, int? brandId = null, int? colorId = null, bool onlyActive = false);

    /// <summary>
    /// Adds a new size(+color) variant to an existing product. If <paramref name="barcode"/> is
    /// blank, a new unique barcode is generated automatically (same series as the barcode generator
    /// page). If <paramref name="sku"/> is blank, one is auto-built as "{ProductCode}-{SizeName}[-{ColorName}]".
    /// </summary>
    Task<Result<int>> CreateAsync(int productId, int sizeId, int? colorId, string? barcode, string? sku, int reorderLevel, int initialQuantity, int? actingUserId);

    Task<Result> UpdateAsync(ProductVariant variant, int? actingUserId);
    Task<Result> DeleteAsync(int productVariantId);

    /// <summary>Looks up a variant by scanned/typed barcode for POS and stock-entry screens.</summary>
    Task<Result<ProductVariant>> LookupByBarcodeAsync(string barcode);
}
