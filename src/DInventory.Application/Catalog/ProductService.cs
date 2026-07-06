using DInventory.Application.Barcoding;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IPriceRepository _priceRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IProductVariantRepository _variantRepository;
    private readonly ISizeRepository _sizeRepository;
    private readonly IColorRepository _colorRepository;
    private readonly IBarcodeNumberGenerator _barcodeNumberGenerator;

    public ProductService(
        IProductRepository productRepository,
        IPriceRepository priceRepository,
        IStockRepository stockRepository,
        IProductVariantRepository variantRepository,
        ISizeRepository sizeRepository,
        IColorRepository colorRepository,
        IBarcodeNumberGenerator barcodeNumberGenerator)
    {
        _productRepository = productRepository;
        _priceRepository = priceRepository;
        _stockRepository = stockRepository;
        _variantRepository = variantRepository;
        _sizeRepository = sizeRepository;
        _colorRepository = colorRepository;
        _barcodeNumberGenerator = barcodeNumberGenerator;
    }

    public Task<Product?> GetByIdAsync(int productId) => _productRepository.GetByIdAsync(productId);

    public Task<PagedResult<Product>> GetPagedAsync(PagedRequest request, int companyId, int? categoryId = null, int? subcategoryId = null, int? brandId = null, bool onlyActive = false)
        => _productRepository.GetPagedAsync(request, companyId, categoryId, subcategoryId, brandId, onlyActive);

    public Task<PagedResult<Product>> GetPublicPagedAsync(PagedRequest request, int? categoryId = null)
        => _productRepository.GetPublicPagedAsync(request, categoryId);

    public Task<IEnumerable<Product>> GetAllAsync(int companyId, bool onlyActive = false) => _productRepository.GetAllAsync(companyId, onlyActive);

    private async Task<string> BuildAutoSkuAsync(string productCode, int sizeId, int? colorId)
    {
        var size = await _sizeRepository.GetByIdAsync(sizeId);
        var parts = new List<string> { productCode, size?.SizeName ?? sizeId.ToString() };
        if (colorId.HasValue)
        {
            var color = await _colorRepository.GetByIdAsync(colorId.Value);
            if (color is not null)
            {
                parts.Add(color.ColorName);
            }
        }

        return string.Join("-", parts).Replace(" ", "").ToUpperInvariant();
    }

    public async Task<Result<int>> CreateAsync(Product product, decimal costPrice, decimal sellingPrice, List<ProductVariantInput> variants, int? actingUserId)
    {
        if (sellingPrice < costPrice)
        {
            return Result<int>.Failure("Selling price cannot be lower than cost price.");
        }

        if (variants is null || variants.Count == 0)
        {
            return Result<int>.Failure("Add at least one size for this product - nothing can be sold without a size/barcode.");
        }

        if (variants.Select(v => (v.SizeId, v.ColorId)).Distinct().Count() != variants.Count)
        {
            return Result<int>.Failure("Each size/color combination can only be added once per product.");
        }

        // Validate any manually supplied barcodes up front so we don't partially create the product.
        foreach (var v in variants)
        {
            if (!string.IsNullOrWhiteSpace(v.Barcode) && await _variantRepository.BarcodeExistsAsync(v.Barcode.Trim()))
            {
                return Result<int>.Failure($"Barcode '{v.Barcode}' is already in use.");
            }
        }

        if (string.IsNullOrWhiteSpace(product.ProductCode))
        {
            product.ProductCode = await _productRepository.GenerateNextProductCodeAsync(product.CompanyId);
        }
        else if (await _productRepository.CodeExistsAsync(product.CompanyId, product.ProductCode))
        {
            return Result<int>.Failure("A product with this code already exists.");
        }

        product.CreatedBy = actingUserId;
        product.CreatedAt = DateTime.UtcNow;
        product.IsActive = true;

        var productId = await _productRepository.CreateAsync(product);

        await _priceRepository.CreateAsync(new Price
        {
            ProductId = productId,
            CostPrice = costPrice,
            SellingPrice = sellingPrice,
            EffectiveFrom = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId
        });

        foreach (var v in variants)
        {
            var barcode = v.Barcode?.Trim();
            if (string.IsNullOrWhiteSpace(barcode))
            {
                barcode = await _barcodeNumberGenerator.GenerateNextAsync(product.CompanyId);
            }

            var sku = string.IsNullOrWhiteSpace(v.SKU) ? await BuildAutoSkuAsync(product.ProductCode, v.SizeId, v.ColorId) : v.SKU.Trim();

            var variantId = await _variantRepository.CreateAsync(new ProductVariant
            {
                ProductId = productId,
                SizeId = v.SizeId,
                ColorId = v.ColorId,
                Barcode = barcode,
                SKU = sku,
                ReorderLevel = v.ReorderLevel <= 0 ? 5 : v.ReorderLevel,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actingUserId
            });

            await _stockRepository.EnsureStockRowExistsAsync(variantId);
            if (v.InitialQuantity > 0)
            {
                await _stockRepository.AdjustQuantityAsync(variantId, v.InitialQuantity);
                await _stockRepository.CreateTransactionAsync(new StockTransaction
                {
                    ProductVariantId = variantId,
                    TransactionType = "IN",
                    Quantity = v.InitialQuantity,
                    ReferenceType = "MANUAL",
                    Remarks = "Initial stock on product creation",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actingUserId
                });
            }
        }

        return Result<int>.Success(productId);
    }

    public async Task<Result> UpdateAsync(Product product, decimal costPrice, decimal sellingPrice, int? actingUserId)
    {
        var existing = await _productRepository.GetByIdAsync(product.ProductId);
        if (existing is null)
        {
            return Result.Failure("Product not found.");
        }

        if (sellingPrice < costPrice)
        {
            return Result.Failure("Selling price cannot be lower than cost price.");
        }

        if (!string.Equals(existing.ProductCode, product.ProductCode, StringComparison.OrdinalIgnoreCase)
            && await _productRepository.CodeExistsAsync(existing.CompanyId, product.ProductCode, product.ProductId))
        {
            return Result.Failure("A product with this code already exists.");
        }

        existing.ProductCode = product.ProductCode;
        existing.ProductName = product.ProductName;
        existing.CategoryId = product.CategoryId;
        existing.SubcategoryId = product.SubcategoryId;
        existing.BrandId = product.BrandId;
        existing.Unit = product.Unit;
        existing.Description = product.Description;
        existing.ReorderLevel = product.ReorderLevel;
        existing.IsShowOnWebsite = product.IsShowOnWebsite;
        existing.ShowPriceOnWebsite = product.ShowPriceOnWebsite;
        existing.IsActive = product.IsActive;
        if (!string.IsNullOrWhiteSpace(product.ImagePath))
        {
            existing.ImagePath = product.ImagePath;
        }
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        var ok = await _productRepository.UpdateAsync(existing);
        if (!ok)
        {
            return Result.Failure("Unable to update product.");
        }

        var activePrice = await _priceRepository.GetActivePriceAsync(product.ProductId);
        if (activePrice is null || activePrice.CostPrice != costPrice || activePrice.SellingPrice != sellingPrice)
        {
            await _priceRepository.DeactivateAllForProductAsync(product.ProductId);
            await _priceRepository.CreateAsync(new Price
            {
                ProductId = product.ProductId,
                CostPrice = costPrice,
                SellingPrice = sellingPrice,
                EffectiveFrom = DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actingUserId
            });
        }

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int productId)
    {
        try
        {
            var ok = await _productRepository.DeleteAsync(productId);
            return ok ? Result.Success() : Result.Failure("Unable to delete product.");
        }
        catch (Exception)
        {
            return Result.Failure("Cannot delete this product because it has related sales or stock history. Consider deactivating it instead.");
        }
    }
}
