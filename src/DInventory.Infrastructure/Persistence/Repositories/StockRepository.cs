using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class StockRepository : IStockRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public StockRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectBase = @"
        SELECT s.StockId, s.ProductVariantId, s.QuantityOnHand, s.UpdatedAt,
               p.ProductName, p.ProductCode, b.BrandName, sz.SizeName, pv.Barcode, pv.ReorderLevel
        FROM dbo.Stock s
        INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = s.ProductVariantId
        INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
        INNER JOIN dbo.Sizes sz ON sz.SizeId = pv.SizeId
        LEFT JOIN dbo.Colors co ON co.ColorId = pv.ColorId
        LEFT JOIN dbo.Brands b ON b.BrandId = p.BrandId";

    public async Task<Stock?> GetByVariantIdAsync(int productVariantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Stock>($"{SelectBase} WHERE s.ProductVariantId = @productVariantId", new { productVariantId });
    }

    public async Task<IEnumerable<Stock>> GetAllAsync(int companyId, bool onlyLowStock = false, string? search = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $@"{SelectBase}
            WHERE (@companyId = 0 OR p.CompanyId = @companyId)
              AND (@onlyLowStock = 0 OR s.QuantityOnHand <= pv.ReorderLevel)
              AND (@search IS NULL OR p.ProductName LIKE @pattern OR p.ProductCode LIKE @pattern OR pv.Barcode LIKE @pattern)
            ORDER BY p.ProductName, sz.DisplayOrder";
        return await connection.QueryAsync<Stock>(sql, new { companyId, onlyLowStock, search, pattern = $"%{search}%" });
    }

    public async Task<PagedResult<Stock>> GetPagedAsync(PagedRequest request, int companyId, int? maxStock = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@companyId = 0 OR p.CompanyId = @companyId)
              AND (@maxStock IS NULL OR s.QuantityOnHand <= @maxStock)
              AND (@search IS NULL OR p.ProductName LIKE @pattern OR p.ProductCode LIKE @pattern OR pv.Barcode LIKE @pattern)";

        var countSql = $@"
            SELECT COUNT(1)
            FROM dbo.Stock s
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = s.ProductVariantId
            INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
            {whereClause}";

        var pagedSql = $@"{SelectBase}
            {whereClause}
            ORDER BY p.ProductName, sz.DisplayOrder
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            companyId,
            maxStock,
            search = request.Search,
            pattern = $"%{request.Search}%",
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Stock>(pagedSql, parameters)).ToList();

        return new PagedResult<Stock>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task EnsureStockRowExistsAsync(int productVariantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM dbo.Stock WHERE ProductVariantId = @productVariantId)
            BEGIN
                INSERT INTO dbo.Stock (ProductVariantId, QuantityOnHand, UpdatedAt) VALUES (@productVariantId, 0, SYSUTCDATETIME());
            END";
        await connection.ExecuteAsync(sql, new { productVariantId });
    }

    public async Task<bool> AdjustQuantityAsync(int productVariantId, int deltaQuantity)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Stock
            SET QuantityOnHand = QuantityOnHand + @deltaQuantity, UpdatedAt = SYSUTCDATETIME()
            WHERE ProductVariantId = @productVariantId";
        var rows = await connection.ExecuteAsync(sql, new { productVariantId, deltaQuantity });
        return rows > 0;
    }

    public async Task<int> CreateTransactionAsync(StockTransaction transaction)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.StockTransactions (ProductVariantId, TransactionType, Quantity, ReferenceType, ReferenceId, Remarks, CreatedAt, CreatedBy)
            OUTPUT INSERTED.TransactionId
            VALUES (@ProductVariantId, @TransactionType, @Quantity, @ReferenceType, @ReferenceId, @Remarks, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, transaction);
    }

    public async Task<IEnumerable<StockTransaction>> GetTransactionsForVariantAsync(int productVariantId, int take = 50)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP (@take) t.TransactionId, t.ProductVariantId, t.TransactionType, t.Quantity, t.ReferenceType,
                   t.ReferenceId, t.Remarks, t.CreatedAt, t.CreatedBy,
                   p.ProductName, sz.SizeName, pv.Barcode, u.FullName AS CreatedByName
            FROM dbo.StockTransactions t
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = t.ProductVariantId
            INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
            INNER JOIN dbo.Sizes sz ON sz.SizeId = pv.SizeId
            LEFT JOIN dbo.Users u ON u.UserId = t.CreatedBy
            WHERE t.ProductVariantId = @productVariantId
            ORDER BY t.CreatedAt DESC";
        return await connection.QueryAsync<StockTransaction>(sql, new { productVariantId, take });
    }

    public async Task<int> GetLowStockCountAsync(int companyId = 0)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT COUNT(1) FROM dbo.Stock s
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = s.ProductVariantId
            INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
            WHERE s.QuantityOnHand <= pv.ReorderLevel AND pv.IsActive = 1 AND p.IsActive = 1
              AND (@companyId = 0 OR p.CompanyId = @companyId)";
        return await connection.ExecuteScalarAsync<int>(sql, new { companyId });
    }

    public async Task<int> GetTotalQuantityForProductAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT ISNULL(SUM(s.QuantityOnHand), 0)
            FROM dbo.Stock s
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = s.ProductVariantId
            WHERE pv.ProductId = @productId";
        return await connection.ExecuteScalarAsync<int>(sql, new { productId });
    }

    public async Task<DateTime?> GetLastSoldAtAsync(int productVariantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT MAX(CreatedAt) FROM dbo.StockTransactions
            WHERE ProductVariantId = @productVariantId AND TransactionType = 'OUT' AND ReferenceType = 'SALE'";
        return await connection.ExecuteScalarAsync<DateTime?>(sql, new { productVariantId });
    }
}
