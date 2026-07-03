using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class ProductVariantRepository : IProductVariantRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductVariantRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectBase = @"
        SELECT pv.ProductVariantId, pv.ProductId, pv.SizeId, pv.ColorId, pv.Barcode, pv.SKU, pv.ReorderLevel,
               pv.IsActive, pv.CreatedAt, pv.CreatedBy,
               p.ProductName, p.ProductCode, b.BrandName, c.CategoryName, sz.SizeName, co.ColorName,
               pr.SellingPrice, pr.CostPrice,
               ISNULL(st.QuantityOnHand, 0) AS QuantityOnHand
        FROM dbo.ProductVariants pv
        INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
        INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
        LEFT JOIN dbo.Brands b ON b.BrandId = p.BrandId
        INNER JOIN dbo.Sizes sz ON sz.SizeId = pv.SizeId
        LEFT JOIN dbo.Colors co ON co.ColorId = pv.ColorId
        LEFT JOIN dbo.Price pr ON pr.ProductId = p.ProductId AND pr.IsActive = 1
        LEFT JOIN dbo.Stock st ON st.ProductVariantId = pv.ProductVariantId";

    public async Task<ProductVariant?> GetByIdAsync(int productVariantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProductVariant>(
            $"{SelectBase} WHERE pv.ProductVariantId = @productVariantId", new { productVariantId });
    }

    public async Task<ProductVariant?> GetByBarcodeAsync(string barcode)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProductVariant>(
            $"{SelectBase} WHERE pv.Barcode = @barcode", new { barcode });
    }

    public async Task<IEnumerable<ProductVariant>> GetByProductIdAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $"{SelectBase} WHERE pv.ProductId = @productId ORDER BY sz.DisplayOrder, sz.SizeName";
        return await connection.QueryAsync<ProductVariant>(sql, new { productId });
    }

    public async Task<PagedResult<ProductVariant>> GetPagedAsync(PagedRequest request, int? categoryId = null, int? brandId = null, int? colorId = null, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@search IS NULL OR p.ProductName LIKE @pattern OR p.ProductCode LIKE @pattern OR pv.Barcode LIKE @pattern)
              AND (@categoryId IS NULL OR p.CategoryId = @categoryId)
              AND (@brandId IS NULL OR p.BrandId = @brandId)
              AND (@colorId IS NULL OR pv.ColorId = @colorId)
              AND (@onlyActive = 0 OR pv.IsActive = 1)";

        var countSql = $@"
            SELECT COUNT(1)
            FROM dbo.ProductVariants pv
            INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
            {whereClause}";

        var pagedSql = $@"{SelectBase}
            {whereClause}
            ORDER BY p.ProductName, sz.DisplayOrder
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            categoryId,
            brandId,
            colorId,
            onlyActive,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<ProductVariant>(pagedSql, parameters)).ToList();

        return new PagedResult<ProductVariant>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(ProductVariant variant)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.ProductVariants (ProductId, SizeId, ColorId, Barcode, SKU, ReorderLevel, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.ProductVariantId
            VALUES (@ProductId, @SizeId, @ColorId, @Barcode, @SKU, @ReorderLevel, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, variant);
    }

    public async Task<bool> UpdateAsync(ProductVariant variant)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.ProductVariants
            SET ProductId = @ProductId, SizeId = @SizeId, ColorId = @ColorId, Barcode = @Barcode, SKU = @SKU,
                ReorderLevel = @ReorderLevel, IsActive = @IsActive
            WHERE ProductVariantId = @ProductVariantId";
        var rows = await connection.ExecuteAsync(sql, variant);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int productVariantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.ProductVariants WHERE ProductVariantId = @productVariantId", new { productVariantId });
        return rows > 0;
    }

    public async Task<bool> BarcodeExistsAsync(string barcode, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.ProductVariants WHERE Barcode = @barcode AND (@excludeId IS NULL OR ProductVariantId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { barcode, excludeId });
        return count > 0;
    }
}
