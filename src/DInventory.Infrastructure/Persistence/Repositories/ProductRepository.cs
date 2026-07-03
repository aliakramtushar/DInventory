using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectBase = @"
        SELECT p.ProductId, p.ProductCode, p.ProductName, p.CategoryId, p.SubcategoryId, p.BrandId, p.Unit,
               p.Description, p.ImagePath, p.ReorderLevel, p.IsShowOnWebsite, p.ShowPriceOnWebsite,
               p.IsActive, p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy,
               c.CategoryName, sc.SubcategoryName, b.BrandName,
               pr.SellingPrice, pr.CostPrice,
               ISNULL(v.VariantCount, 0) AS VariantCount,
               ISNULL(v.TotalQuantityOnHand, 0) AS TotalQuantityOnHand
        FROM dbo.Products p
        INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
        LEFT JOIN dbo.Subcategories sc ON sc.SubcategoryId = p.SubcategoryId
        LEFT JOIN dbo.Brands b ON b.BrandId = p.BrandId
        LEFT JOIN dbo.Price pr ON pr.ProductId = p.ProductId AND pr.IsActive = 1
        OUTER APPLY (
            SELECT COUNT(1) AS VariantCount, SUM(ISNULL(st.QuantityOnHand, 0)) AS TotalQuantityOnHand
            FROM dbo.ProductVariants pv
            LEFT JOIN dbo.Stock st ON st.ProductVariantId = pv.ProductVariantId
            WHERE pv.ProductId = p.ProductId
        ) v";

    public async Task<Product?> GetByIdAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Product>($"{SelectBase} WHERE p.ProductId = @productId", new { productId });
    }

    public async Task<Product?> GetByCodeAsync(string productCode)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Product>($"{SelectBase} WHERE p.ProductCode = @productCode", new { productCode });
    }

    public async Task<PagedResult<Product>> GetPagedAsync(PagedRequest request, int? categoryId = null, int? subcategoryId = null, int? brandId = null, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@search IS NULL OR p.ProductName LIKE @pattern OR p.ProductCode LIKE @pattern)
              AND (@categoryId IS NULL OR p.CategoryId = @categoryId)
              AND (@subcategoryId IS NULL OR p.SubcategoryId = @subcategoryId)
              AND (@brandId IS NULL OR p.BrandId = @brandId)
              AND (@onlyActive = 0 OR p.IsActive = 1)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Products p {whereClause}";
        var pagedSql = $@"{SelectBase}
            {whereClause}
            ORDER BY p.ProductName
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            categoryId,
            subcategoryId,
            brandId,
            onlyActive,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Product>(pagedSql, parameters)).ToList();

        return new PagedResult<Product>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<PagedResult<Product>> GetPublicPagedAsync(PagedRequest request, int? categoryId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE p.IsShowOnWebsite = 1
              AND p.IsActive = 1
              AND (@search IS NULL OR p.ProductName LIKE @pattern OR p.ProductCode LIKE @pattern)
              AND (@categoryId IS NULL OR p.CategoryId = @categoryId)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Products p {whereClause}";
        var pagedSql = $@"{SelectBase}
            {whereClause}
            ORDER BY p.ProductName
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            categoryId,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Product>(pagedSql, parameters)).ToList();

        return new PagedResult<Product>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<IEnumerable<Product>> GetAllAsync(bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $"{SelectBase} WHERE (@onlyActive = 0 OR p.IsActive = 1) ORDER BY p.ProductName";
        return await connection.QueryAsync<Product>(sql, new { onlyActive });
    }

    public async Task<int> CreateAsync(Product product)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Products (ProductCode, ProductName, CategoryId, SubcategoryId, BrandId, Unit, Description,
                                       ImagePath, ReorderLevel, IsShowOnWebsite, ShowPriceOnWebsite, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.ProductId
            VALUES (@ProductCode, @ProductName, @CategoryId, @SubcategoryId, @BrandId, @Unit, @Description,
                    @ImagePath, @ReorderLevel, @IsShowOnWebsite, @ShowPriceOnWebsite, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, product);
    }

    public async Task<bool> UpdateAsync(Product product)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Products
            SET ProductCode = @ProductCode, ProductName = @ProductName, CategoryId = @CategoryId,
                SubcategoryId = @SubcategoryId, BrandId = @BrandId, Unit = @Unit, Description = @Description,
                ImagePath = @ImagePath, ReorderLevel = @ReorderLevel, IsShowOnWebsite = @IsShowOnWebsite,
                ShowPriceOnWebsite = @ShowPriceOnWebsite, IsActive = @IsActive,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE ProductId = @ProductId";
        var rows = await connection.ExecuteAsync(sql, product);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Products WHERE ProductId = @productId", new { productId });
        return rows > 0;
    }

    public async Task<bool> CodeExistsAsync(string code, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Products WHERE ProductCode = @code AND (@excludeId IS NULL OR ProductId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { code, excludeId });
        return count > 0;
    }

    public async Task<int> GetTotalActiveCountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Products WHERE IsActive = 1");
    }

    public async Task<string> GenerateNextProductCodeAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP 1 ProductCode FROM dbo.Products
            WHERE ProductCode LIKE 'PRD-%'
            ORDER BY ProductId DESC";
        var lastCode = await connection.QuerySingleOrDefaultAsync<string>(sql);

        var nextNumber = 1;
        if (!string.IsNullOrEmpty(lastCode) && lastCode.Contains('-'))
        {
            var numericPart = lastCode.Split('-').Last();
            if (int.TryParse(numericPart, out var parsed))
            {
                nextNumber = parsed + 1;
            }
        }

        return $"PRD-{nextNumber:D4}";
    }
}
