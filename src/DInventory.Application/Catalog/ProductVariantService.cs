using DInventory.Application.Barcoding;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Catalog;

public class ProductVariantService : IProductVariantService
{
    private readonly IProductVariantRepository _variantRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ISizeRepository _sizeRepository;
    private readonly IColorRepository _colorRepository;
    private readonly IBarcodeNumberGenerator _barcodeNumberGenerator;
    private readonly IGeneratedBarcodeLabelService _labelService;

    public ProductVariantService(
        IProductVariantRepository variantRepository,
        IProductRepository productRepository,
        IStockRepository stockRepository,
        ISizeRepository sizeRepository,
        IColorRepository colorRepository,
        IBarcodeNumberGenerator barcodeNumberGenerator,
        IGeneratedBarcodeLabelService labelService)
    {
        _variantRepository = variantRepository;
        _productRepository = productRepository;
        _stockRepository = stockRepository;
        _sizeRepository = sizeRepository;
        _colorRepository = colorRepository;
        _barcodeNumberGenerator = barcodeNumberGenerator;
        _labelService = labelService;
    }

    public Task<ProductVariant?> GetByIdAsync(int productVariantId) => _variantRepository.GetByIdAsync(productVariantId);

    public Task<ProductVariant?> GetByBarcodeAsync(string barcode) => _variantRepository.GetByBarcodeAsync(barcode.Trim());

    public Task<IEnumerable<ProductVariant>> GetByProductIdAsync(int productId) => _variantRepository.GetByProductIdAsync(productId);

    public Task<PagedResult<ProductVariant>> GetPagedAsync(PagedRequest request, int companyId = 0, int? categoryId = null, int? brandId = null, int? colorId = null, bool onlyActive = false)
        => _variantRepository.GetPagedAsync(request, companyId, categoryId, brandId, colorId, onlyActive);

    /// <summary>Shared by product-creation and add-variant-later flows so both build the exact same
    /// "{ProductCode}-{SizeName}[-{ColorName}]" SKU when the user leaves SKU blank.</summary>
    internal async Task<string> BuildAutoSkuAsync(string productCode, int sizeId, int? colorId)
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

    public async Task<Result<int>> CreateAsync(int productId, int sizeId, int? colorId, string? barcode, string? sku, int reorderLevel, int initialQuantity, int? actingUserId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product is null)
        {
            return Result<int>.Failure("Product not found.");
        }

        var code = barcode?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            code = await _barcodeNumberGenerator.GenerateNextAsync(product.CompanyId);
        }
        else if (await _variantRepository.BarcodeExistsAsync(code))
        {
            return Result<int>.Failure($"Barcode '{code}' is already assigned to another product/size.");
        }

        var resolvedSku = string.IsNullOrWhiteSpace(sku) ? await BuildAutoSkuAsync(product.ProductCode, sizeId, colorId) : sku.Trim();

        var variant = new ProductVariant
        {
            ProductId = productId,
            SizeId = sizeId,
            ColorId = colorId,
            Barcode = code,
            SKU = resolvedSku,
            ReorderLevel = reorderLevel <= 0 ? 5 : reorderLevel,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId
        };

        int variantId;
        try
        {
            variantId = await _variantRepository.CreateAsync(variant);
        }
        catch (Exception)
        {
            return Result<int>.Failure("This product already has a variant in that size/color, or the barcode is a duplicate.");
        }

        // If this barcode was printed ahead of time via the Barcode Generator, flip its label over to
        // "Linked" now that it's actually been entered into inventory (no-op if it wasn't printed there).
        await _labelService.MarkLinkedAsync(code, variantId);

        await _stockRepository.EnsureStockRowExistsAsync(variantId);
        if (initialQuantity > 0)
        {
            await _stockRepository.AdjustQuantityAsync(variantId, initialQuantity);
            await _stockRepository.CreateTransactionAsync(new StockTransaction
            {
                ProductVariantId = variantId,
                TransactionType = "IN",
                Quantity = initialQuantity,
                ReferenceType = "MANUAL",
                Remarks = "Initial stock on variant creation",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = actingUserId
            });
        }

        return Result<int>.Success(variantId);
    }

    public async Task<Result> UpdateAsync(ProductVariant variant, int? actingUserId)
    {
        var existing = await _variantRepository.GetByIdAsync(variant.ProductVariantId);
        if (existing is null)
        {
            return Result.Failure("Product variant not found.");
        }

        if (!string.Equals(existing.Barcode, variant.Barcode, StringComparison.OrdinalIgnoreCase)
            && await _variantRepository.BarcodeExistsAsync(variant.Barcode, variant.ProductVariantId))
        {
            return Result.Failure($"Barcode '{variant.Barcode}' is already assigned to another product/size.");
        }

        var barcodeChanged = !string.Equals(existing.Barcode, variant.Barcode, StringComparison.OrdinalIgnoreCase);

        existing.SizeId = variant.SizeId;
        existing.ColorId = variant.ColorId;
        existing.Barcode = variant.Barcode.Trim();
        existing.SKU = variant.SKU;
        existing.ReorderLevel = variant.ReorderLevel <= 0 ? 5 : variant.ReorderLevel;
        existing.IsActive = variant.IsActive;

        var ok = await _variantRepository.UpdateAsync(existing);
        if (ok && barcodeChanged)
        {
            // The barcode changed to (possibly) a previously printed label - reflect that on its History row.
            await _labelService.MarkLinkedAsync(existing.Barcode, existing.ProductVariantId);
        }

        return ok ? Result.Success() : Result.Failure("Unable to update product variant.");
    }

    public async Task<Result> DeleteAsync(int productVariantId)
    {
        try
        {
            var ok = await _variantRepository.DeleteAsync(productVariantId);
            return ok ? Result.Success() : Result.Failure("Unable to delete this product/size variant.");
        }
        catch (Exception)
        {
            return Result.Failure("Cannot delete this variant because it has related sales or stock history. Consider deactivating it instead.");
        }
    }

    public async Task<Result<ProductVariant>> LookupByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return Result<ProductVariant>.Failure("Please scan or enter a barcode.");
        }

        var variant = await _variantRepository.GetByBarcodeAsync(barcode.Trim());
        if (variant is null)
        {
            return Result<ProductVariant>.Failure($"No product found for barcode '{barcode}'.");
        }

        if (!variant.IsActive)
        {
            return Result<ProductVariant>.Failure($"'{variant.ProductName} ({variant.SizeName})' is deactivated and cannot be sold.");
        }

        return Result<ProductVariant>.Success(variant);
    }
}
