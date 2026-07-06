using DInventory.Application.Catalog;
using DInventory.Application.Common.Models;
using DInventory.Application.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    private readonly IContentPageService _contentPageService;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;

    public HomeController(IContentPageService contentPageService, IProductService productService, ICategoryService categoryService)
    {
        _contentPageService = contentPageService;
        _productService = productService;
        _categoryService = categoryService;
    }

    public async Task<IActionResult> Index()
    {
        var home = await _contentPageService.GetBySlugAsync("home");
        var pages = (await _contentPageService.GetAllAsync(onlyPublished: true)).ToList();
        ViewBag.Pages = pages;
        return View(home);
    }

    public async Task<IActionResult> About()
    {
        ViewBag.Pages = (await _contentPageService.GetAllAsync(onlyPublished: true)).ToList();
        ViewData["Title"] = "About Us";
        return View();
    }

    public async Task<IActionResult> Page(string slug)
    {
        var page = await _contentPageService.GetBySlugAsync(slug);
        if (page is null || !page.IsPublished)
        {
            return NotFound();
        }

        var pages = (await _contentPageService.GetAllAsync(onlyPublished: true)).ToList();
        ViewBag.Pages = pages;
        return View(page);
    }

    /// <summary>Public storefront product grid: only products with IsShowOnWebsite=1 are ever
    /// returned by GetPublicPagedAsync, and each product's ShowPriceOnWebsite flag decides whether
    /// the view shows a price or a "Contact us for price" message. Store-front display only - no
    /// cart/checkout here.</summary>
    public async Task<IActionResult> Products(int? categoryId, string? search, int page = 1)
    {
        var request = new PagedRequest { PageNumber = page, PageSize = 12, Search = search };
        var result = await _productService.GetPublicPagedAsync(request, categoryId);

        ViewBag.Pages = (await _contentPageService.GetAllAsync(onlyPublished: true)).ToList();
        // Public storefront is shared across every tenant on this install (not yet scoped per
        // company), so companyId 0 here means "show every company's categories", same bypass
        // semantics as the admin-side superuser - not an actual superuser request.
        ViewBag.Categories = await _categoryService.GetAllAsync(companyId: 0, onlyActive: true);
        ViewData["CategoryId"] = categoryId;
        ViewData["Search"] = search;
        ViewData["Title"] = "Our Products";

        return View(result);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
