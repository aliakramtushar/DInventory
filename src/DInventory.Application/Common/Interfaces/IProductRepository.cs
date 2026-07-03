using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Common.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int productId);
    Task<Product?> GetByCodeAsync(string productCode);
    Task<PagedResult<Product>> GetPagedAsync(PagedRequest request, int? categoryId = null, int? subcategoryId = null, int? brandId = null, bool onlyActive = false);
    Task<PagedResult<Product>> GetPublicPagedAsync(PagedRequest request, int? categoryId = null);
    Task<IEnumerable<Product>> GetAllAsync(bool onlyActive = false);
    Task<int> CreateAsync(Product product);
    Task<bool> UpdateAsync(Product product);
    Task<bool> DeleteAsync(int productId);
    Task<bool> CodeExistsAsync(string code, int? excludeId = null);
    Task<int> GetTotalActiveCountAsync();
    Task<string> GenerateNextProductCodeAsync();
}
