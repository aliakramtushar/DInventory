using DInventory.Application.Audit;
using DInventory.Application.Catalog;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Application.Tenancy;
using DInventory.Domain.Entities;
using DInventory.Domain.Enums;
using DInventory.Web.Common;
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
    private readonly IBusinessUnitService _businessUnitService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitContextService _businessUnitContextService;
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
        IBusinessUnitService businessUnitService,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitContextService businessUnitContextService,
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
        _businessUnitService = businessUnitService;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitContextService = businessUnitContextService;
        _auditLogService = auditLogService;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<IActionResult> Index(int? categoryId, int? brandId, int? businessUnitId, string? search, int page = 1)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();

        var request = new PagedRequest { PageNumber = page, PageSize = 20, Search = search };
        var result = await _productService.GetPagedAsync(request, effectiveCompanyId, categoryId, brandId: brandId, businessUnitId: businessUnitId);

        ViewData["Search"] = search;
        ViewData["CategoryId"] = categoryId;
        ViewData["BrandId"] = brandId;
        ViewData["BusinessUnitId"] = businessUnitId;
        ViewData["CompanyId"] = effectiveCompanyId;
        ViewBag.IsSuperCompany = currentUser.IsSuperCompany;
        ViewBag.Categories = await _categoryService.GetAllAsync(effectiveCompanyId, onlyActive: true);
        ViewBag.Brands = await _brandService.GetAllAsync(effectiveCompanyId, onlyActive: true);

        ViewBag.BusinessUnits = effectiveCompanyId > 0
            ? await _businessUnitService.GetAllAsync(effectiveCompanyId, onlyActive: true)
            : Enumerable.Empty<DInventory.Domain.Entities.BusinessUnit>();

        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && product.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        ViewBag.Variants = await _productVariantService.GetByProductIdAsync(id);
        return View(product);
    }

    [HttpGet]
    [PermissionAuthorize("PRODUCTS", PermissionAction.Create)]
    public async Task<IActionResult> Create()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        await PopulateDropdownsAsync(effectiveCompanyId);
        return View(new Product { CompanyId = effectiveCompanyId, BusinessUnitId = currentUser.BusinessUnitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("PRODUCTS", PermissionAction.Create)]
    public async Task<IActionResult> Create(Product model, decimal costPrice, decimal sellingPrice, IFormFile? imageFile,
        List<int>? variantSizeId, List<int?>? variantColorId, List<string?>? variantBarcode, List<string?>? variantSku,
        List<int?>? variantReorderLevel, List<int?>? variantInitialQuantity)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        ViewBag.CanCreate = effectiveCompanyId > 0;
        model.CompanyId = effectiveCompanyId;
        model.BusinessUnitId = await _businessUnitContextService.GetEffectiveBusinessUnitIdAsync();

        var variants = BuildVariantInputs(variantSizeId, variantColorId, variantBarcode, variantSku, variantReorderLevel, variantInitialQuantity);

        if (variants.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one size for this product - nothing can be sold without a size/barcode.");
        }

        if (model.CompanyId <= 0)
        {
            ModelState.AddModelError(string.Empty, "Select a company from the Company dropdown in the top navigation bar before creating a product.");
        }

        if (imageFile is { Length: > 0 } && !ImageUploadValidator.TryValidate(imageFile, out var imageError))
        {
            ModelState.AddModelError(string.Empty, imageError!);
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(effectiveCompanyId > 0 ? effectiveCompanyId : model.CompanyId);
            return View(model);
        }

        if (imageFile is { Length: > 0 })
        {
            model.ImagePath = await SaveProductImageAsync(imageFile);
        }

        var result = await _productService.CreateAsync(model, costPrice, sellingPrice, variants, currentUser.UserId);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create product.");
            await PopulateDropdownsAsync(effectiveCompanyId > 0 ? effectiveCompanyId : model.CompanyId);
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

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && product.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        await PopulateDropdownsAsync(product.CompanyId);
        ViewBag.Variants = await _productVariantService.GetByProductIdAsync(id);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("PRODUCTS", PermissionAction.Edit)]
    public async Task<IActionResult> Edit(Product model, decimal costPrice, decimal sellingPrice, IFormFile? imageFile)
    {
        if (imageFile is { Length: > 0 } && !ImageUploadValidator.TryValidate(imageFile, out var imageError))
        {
            ModelState.AddModelError(string.Empty, imageError!);
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model.CompanyId);
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
            await PopulateDropdownsAsync(model.CompanyId);
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
        var product = await _productService.GetByIdAsync(productId);
        if (product is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && product.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

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
        var product = await _productService.GetByIdAsync(productId);
        if (product is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && product.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

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
        var product = await _productService.GetByIdAsync(productId);
        if (product is null)
        {
            return NotFound();
        }

        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && product.CompanyId != currentUser.CompanyId)
        {
            return Forbid();
        }

        var result = await _productVariantService.DeleteAsync(productVariantId);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await _auditLogService.LogAsync(currentUser.UserId, currentUser.Username, "DELETE", "ProductVariants", productVariantId.ToString(), ipAddress: currentUser.IpAddress);
            TempData["StatusMessage"] = "Size/variant removed.";
        }

        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    // NOTE ON THE FIX BELOW: variantReorderLevel/variantInitialQuantity used to be bound as
    // List<int> (non-nullable). ASP.NET Core's default model binder for a repeated-name,
    // non-indexed collection like this silently DROPS any element whose raw form value fails to
    // convert (e.g. an emptied-out number box) instead of defaulting it to 0 - it does not throw
    // and does not pad the list. With multiple size/variant rows, that silently shifted every
    // later row's quantity/reorder value onto the wrong row (or lost it entirely), which is
    // exactly what was reported as "stock doesn't work properly when adding a product". Binding
    // as List<int?> makes an empty box bind to null (never dropped), so the lists always stay
    // index-aligned with variantSizeId. The JS below also fills any blank qty/reorder box with
    // its default right before submit, so a blank value should no longer occur at all in practice.
    private static List<ProductVariantInput> BuildVariantInputs(
        List<int>? variantSizeId, List<int?>? variantColorId, List<string?>? variantBarcode, List<string?>? variantSku,
        List<int?>? variantReorderLevel, List<int?>? variantInitialQuantity)
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

            var reorderLevel = variantReorderLevel != null && i < variantReorderLevel.Count ? variantReorderLevel[i] : null;
            var initialQuantity = variantInitialQuantity != null && i < variantInitialQuantity.Count ? variantInitialQuantity[i] : null;

            variants.Add(new ProductVariantInput
            {
                SizeId = variantSizeId[i],
                ColorId = variantColorId != null && i < variantColorId.Count && variantColorId[i] > 0 ? variantColorId[i] : null,
                Barcode = variantBarcode != null && i < variantBarcode.Count ? variantBarcode[i] : null,
                SKU = variantSku != null && i < variantSku.Count ? variantSku[i] : null,
                ReorderLevel = (reorderLevel ?? 0) <= 0 ? 5 : reorderLevel!.Value,
                InitialQuantity = (initialQuantity ?? 0) < 0 ? 0 : (initialQuantity ?? 0)
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

    /// <summary>Populates the Category/Subcategory/Brand/Size/Color pickers for the given company -
    /// for Create that's the globally-selected effective company; for Edit it's always the record's
    /// own (fixed) CompanyId, since editing never changes which company a product belongs to.</summary>
    private async Task PopulateDropdownsAsync(int companyId)
    {
        ViewBag.Categories = await _categoryService.GetAllAsync(companyId, onlyActive: true);
        ViewBag.Subcategories = await _subcategoryService.GetAllAsync(companyId, onlyActive: true);
        ViewBag.Brands = await _brandService.GetAllAsync(companyId, onlyActive: true);
        ViewBag.Sizes = await _sizeService.GetAllAsync(companyId, onlyActive: true);
        ViewBag.Colors = await _colorService.GetAllAsync(companyId, onlyActive: true);
    }
}
