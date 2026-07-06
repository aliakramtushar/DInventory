using System.Text.RegularExpressions;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Content;

public partial class ContentPageService : IContentPageService
{
    private readonly IContentPageRepository _contentPageRepository;

    public ContentPageService(IContentPageRepository contentPageRepository)
    {
        _contentPageRepository = contentPageRepository;
    }

    public Task<ContentPage?> GetByIdAsync(int contentPageId) => _contentPageRepository.GetByIdAsync(contentPageId);

    public Task<ContentPage?> GetBySlugAsync(string slug) => _contentPageRepository.GetBySlugAsync(slug);

    public Task<IEnumerable<ContentPage>> GetAllAsync(bool onlyPublished = false) => _contentPageRepository.GetAllAsync(onlyPublished);

    public Task<PagedResult<ContentPage>> GetPagedAsync(PagedRequest request, int companyId) => _contentPageRepository.GetPagedAsync(request, companyId);

    public async Task<Result<int>> CreateAsync(ContentPage page, int? actingUserId)
    {
        page.Slug = string.IsNullOrWhiteSpace(page.Slug) ? Slugify(page.Title) : Slugify(page.Slug);

        if (await _contentPageRepository.SlugExistsAsync(page.Slug))
        {
            page.Slug = $"{page.Slug}-{DateTime.UtcNow.Ticks % 10000}";
        }

        page.CreatedBy = actingUserId;
        page.CreatedAt = DateTime.UtcNow;

        var id = await _contentPageRepository.CreateAsync(page);
        return Result<int>.Success(id);
    }

    public async Task<Result> UpdateAsync(ContentPage page, int? actingUserId)
    {
        var existing = await _contentPageRepository.GetByIdAsync(page.ContentPageId);
        if (existing is null)
        {
            return Result.Failure("Page not found.");
        }

        var newSlug = string.IsNullOrWhiteSpace(page.Slug) ? Slugify(page.Title) : Slugify(page.Slug);
        if (!newSlug.Equals(existing.Slug, StringComparison.OrdinalIgnoreCase)
            && await _contentPageRepository.SlugExistsAsync(newSlug, page.ContentPageId))
        {
            return Result.Failure("A page with this URL slug already exists.");
        }

        existing.Title = page.Title;
        existing.Slug = newSlug;
        existing.Body = page.Body;
        existing.IsPublished = page.IsPublished;
        existing.DisplayOrder = page.DisplayOrder;
        if (!string.IsNullOrWhiteSpace(page.ImagePath))
        {
            existing.ImagePath = page.ImagePath;
        }
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        var ok = await _contentPageRepository.UpdateAsync(existing);
        return ok ? Result.Success() : Result.Failure("Unable to update page.");
    }

    public async Task<Result> DeleteAsync(int contentPageId)
    {
        var ok = await _contentPageRepository.DeleteAsync(contentPageId);
        return ok ? Result.Success() : Result.Failure("Unable to delete page.");
    }

    private static string Slugify(string input)
    {
        var value = input.Trim().ToLowerInvariant();
        value = NonAlphaNumericRegex().Replace(value, "-");
        value = MultipleDashesRegex().Replace(value, "-").Trim('-');
        return string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N")[..8] : value;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultipleDashesRegex();
}
