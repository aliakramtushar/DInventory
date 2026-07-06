using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public interface IProductService
{
    Task<Product?> GetByIdAsync(int productId);
    Task<PagedResult<Product>> GetPagedAsync(PagedRequest request, int companyId, int? categoryId = null, int? subcategoryId = null, int? brandId = null, bool onlyActive = false);
    Task<PagedResult<Product>> GetPublicPagedAsync(PagedRequest request, int? categoryId = null);
    Task<IEnumerable<Product>> GetAllAsync(int companyId, bool onlyActive = false);

    /// <summary>
    /// Creates a product (a style, e.g. "Classic Crew T-Shirt") along with its price and one or more
    /// size variants. At least one variant is required since nothing is sellable without a size/barcode.
    /// </summary>
    Task<Result<int>> CreateAsync(Product product, decimal costPrice, decimal sellingPrice, List<ProductVariantInput> variants, int? actingUserId);

    Task<Result> UpdateAsync(Product product, decimal costPrice, decimal sellingPrice, int? actingUserId);
    Task<Result> DeleteAsync(int productId);
}
