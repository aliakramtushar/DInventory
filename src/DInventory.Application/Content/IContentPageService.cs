using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Content;

public interface IContentPageService
{
    Task<ContentPage?> GetByIdAsync(int contentPageId);
    Task<ContentPage?> GetBySlugAsync(string slug);
    Task<IEnumerable<ContentPage>> GetAllAsync(bool onlyPublished = false);
    Task<PagedResult<ContentPage>> GetPagedAsync(PagedRequest request, int companyId);
    Task<Result<int>> CreateAsync(ContentPage page, int? actingUserId);
    Task<Result> UpdateAsync(ContentPage page, int? actingUserId);
    Task<Result> DeleteAsync(int contentPageId);
}
