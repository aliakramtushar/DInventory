using DInventory.Application.Audit;
using DInventory.Application.Catalog;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using DInventory.Domain.Enums;
using DInventory.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[Authorize]
[PermissionAuthorize("PRODUCTS")]
public class ProductsController : Controller
{
    private readonly IProductService _productService;
    private readonly IProductVariantService _productVariantService;
    private readonly ICategoryService _categoryService;
    private readonly ISubcategoryService _subcategoryService;
    private readonly IBrandService _brandService;
    private readonly ISizeService _sizeService;
    private readonly IColorService _colorService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IWebHostEnvironment _hostEnvironment;

    public ProductsController(
        IProductService productService,
        IProductVariantService productVariantService,
        ICategoryService categoryService,
        ISubcategoryService subcategoryService,
        IBrandService brandService,
        ISizeService sizeService,
        IColorService colorService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IWebHostEnvironment hostEnvironment)
    {
        _productService = productService;
        _productVariantService = productVariantService;
        _categoryService = categoryService;
        _subcategoryService = subcategoryService;
        _brandService = brandService;
        _sizeService = sizeService;
        _colorService = colorService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<IActionResult> Index(int? categoryId, int? brandId, string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _productService.GetPagedAsync(request, categoryId, brandId: brandId);

        ViewData["Search"] = search;
        ViewData["CategoryId"] = categoryId;
        ViewData["BrandId"] = brandId;
        ViewBag.Categories = await _categoryService.GetAllAsync(onlyActive: true);
        ViewBag.Brands = await _brandService.GetAllAsync(onlyActive: true);

        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        ViewBag.Variants = await _productVariantService.GetByProductIdAsync(id);
        return View(product);
    }

    [HttpGet]
    [PermissionAuthorize("PRODUCTS", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View(new Product());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("PRODUCTS", PermissionAction.Create)]
    public async Task<IActionResult> Create(Product model, decimal costPrice, decimal sellingPrice, IFormFile? imageFile,
        List<int>? variantSizeId, List<int?>? variantColorId, List<string?>? variantBarcode, List<string?>? variantSku,
        List<int>? variantReorderLevel, List<int>? variantInitialQuantity)
    {
        var variants = BuildVariantInputs(variantSizeId, variantColorId, variantBarcode, variantSku, variantReorderLevel, variantInitialQuantity);

        if (variants.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one size for this product - nothing can be sold without a size/barcode.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(model);
        }

        if (imageFile is { Length: > 0 })
        {
            model.ImagePath = await SaveProductImageAsync(imageFile);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _productService.CreateAsync(model, costPrice, sellingPrice, variants, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create product.");
            await PopulateDropdownsAsync();
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "Products", result.Data.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Product created.";
        return RedirectToAction(nameof(Details), new { id = result.Data });
    }

    [HttpGet]
    [PermissionAuthorize("PRODUCTS", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        await PopulateDropdownsAsync();
        ViewBag.Variants = await _productVariantService.GetByProductIdAsync(id);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("PRODUCTS", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(Product model, decimal costPrice, decimal sellingPrice, IFormFile? imageFile)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            ViewBag.Variants = await _productVariantService.GetByProductIdAsync(model.ProductId);
            return View(model);
        }

        if (imageFile is { Length: > 0 })
        {
            model.ImagePath = await SaveProductImageAsync(imageFile);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _productService.UpdateAsync(model, costPrice, sellingPrice, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update product.");
            await PopulateDropdownsAsync();
            ViewBag.Variants = await _productVariantService.GetByProductIdAsync(model.ProductId);
            return View(model);
        }

        await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "Products", model.ProductId.ToString(), ipAddress: currentUser.IpAddress);
        TempData["StatusMessage"] = "Product updated.";
        return RedirectToAction(nameof(Edit), new { id = model.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("PRODUCTS", PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _productService.DeleteAsync(id);
        var currentUser = _currentUserService.GetCurrentUser();

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "Products", id.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Product deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ---- Variant management on an existing product (add / edit / deactivate a size) -------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("PRODUCTS", PermissionAction.Edit)]
    public async Task<IActionResult> AddVariant(int productId, int sizeId, int? colorId, string? barcode, string? sku, int reorderLevel, int initialQuantity)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _productVariantService.CreateAsync(productId, sizeId, colorId, barcode, sku, reorderLevel, initialQuantity, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "CREATE", "ProductVariants", result.Data.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Size/variant added.";
        }

        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("PRODUCTS", PermissionAction.Edit)]
    public async Task<IActionResult> UpdateVariant(int productVariantId, int productId, int sizeId, int? colorId, string barcode, string? sku, int reorderLevel, bool isActive)
    {
        var variant = await _productVariantService.GetByIdAsync(productVariantId);
        if (variant is null)
        {
            return NotFound();
        }

        variant.SizeId = sizeId;
        variant.ColorId = colorId;
        variant.Barcode = barcode;
        variant.SKU = sku;
        variant.ReorderLevel = reorderLevel;
        variant.IsActive = isActive;

        var currentUser = _currentUserService.GetCurrentUser();
        var result = await _productVariantService.UpdateAsync(variant, currentUser.UserId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "UPDATE", "ProductVariants", productVariantId.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Size/variant updated.";
        }

        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("PRODUCTS", PermissionAction.Delete)]
    public async Task<IActionResult> DeleteVariant(int productVariantId, int productId)
    {
        var result = await _productVariantService.DeleteAsync(productVariantId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            var currentUser = _currentUserService.GetCurrentUser();
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "ProductVariants", productVariantId.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Size/variant removed.";
        }

        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    private static List<ProductVariantInput> BuildVariantInputs(
        List<int>? variantSizeId, List<int?>? variantColorId, List<string?>? variantBarcode, List<string?>? variantSku,
        List<int>? variantReorderLevel, List<int>? variantInitialQuantity)
    {
        var variants = new List<ProductVariantInput>();
        if (variantSizeId is null)
        {
            return variants;
        }

        for (var i = 0; i < variantSizeId.Count; i++)
        {
            if (variantSizeId[i] <= 0)
            {
                continue;
            }

            variants.Add(new ProductVariantInput
            {
                SizeId = variantSizeId[i],
                ColorId = variantColorId != null && i < variantColorId.Count && variantColorId[i] > 0 ? variantColorId[i] : null,
                Barcode = variantBarcode != null && i < variantBarcode.Count ? variantBarcode[i] : null,
                SKU = variantSku != null && i < variantSku.Count ? variantSku[i] : null,
                ReorderLevel = variantReorderLevel != null && i < variantReorderLevel.Count && variantReorderLevel[i] > 0 ? variantReorderLevel[i] : 5,
                InitialQuantity = variantInitialQuantity != null && i < variantInitialQuantity.Count ? variantInitialQuantity[i] : 0
            });
        }

        return variants;
    }

    private async Task<string> SaveProductImageAsync(IFormFile file)
    {
        var uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/products/{fileName}";
    }

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.Categories = await _categoryService.GetAllAsync(onlyActive: true);
        ViewBag.Subcategories = await _subcategoryService.GetAllAsync(onlyActive: true);
        ViewBag.Brands = await _brandService.GetAllAsync(onlyActive: true);
        ViewBag.Sizes = await _sizeService.GetAllAsync(onlyActive: true);
        ViewBag.Colors = await _colorService.GetAllAsync(onlyActive: true);
    }
}
