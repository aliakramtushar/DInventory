using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IContentPageRepository
{
    Task<ContentPage?> GetByIdAsync(int contentPageId);
    Task<ContentPage?> GetBySlugAsync(string slug);
    Task<IEnumerable<ContentPage>> GetAllAsync(bool onlyPublished = false);
    Task<PagedResult<ContentPage>> GetPagedAsync(PagedRequest request);
    Task<int> CreateAsync(ContentPage page);
    Task<bool> UpdateAsync(ContentPage page);
    Task<bool> DeleteAsync(int contentPageId);
    Task<bool> SlugExistsAsync(string slug, int? excludeId = null);
}
