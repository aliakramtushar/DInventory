using System.Data.Common;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class SalesReturnRepository : ISalesReturnRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SalesReturnRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string HeaderSelect = @"
        SELECT sr.SalesReturnId, sr.ReturnNo, sr.SalesOrderId, sr.ReturnDate, sr.SubTotal, sr.NetAmount,
               sr.Reason, sr.Remarks, sr.CreatedAt, sr.CreatedBy,
               so.InvoiceNo, c.CustomerName, u.FullName AS CreatedByName
        FROM dbo.SalesReturns sr
        INNER JOIN dbo.SalesOrders so ON so.SalesOrderId = sr.SalesOrderId
        LEFT JOIN dbo.Customers c ON c.CustomerId = so.CustomerId
        INNER JOIN dbo.Users u ON u.UserId = sr.CreatedBy";

    public async Task<SalesReturn?> GetByIdAsync(int salesReturnId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var salesReturn = await connection.QuerySingleOrDefaultAsync<SalesReturn>(
            $"{HeaderSelect} WHERE sr.SalesReturnId = @salesReturnId", new { salesReturnId });

        if (salesReturn is null)
        {
            return null;
        }

        const string itemsSql = @"
            SELECT sri.SalesReturnItemId, sri.SalesReturnId, sri.SalesOrderItemId, sri.ProductVariantId,
                   sri.Quantity, sri.UnitPrice, sri.LineTotal,
                   p.ProductName, p.ProductCode, sz.SizeName, pv.Barcode
            FROM dbo.SalesReturnItems sri
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = sri.ProductVariantId
            INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
            INNER JOIN dbo.Sizes sz ON sz.SizeId = pv.SizeId
            WHERE sri.SalesReturnId = @salesReturnId";

        var items = await connection.QueryAsync<SalesReturnItem>(itemsSql, new { salesReturnId });
        salesReturn.Items = items.ToList();

        return salesReturn;
    }

    public async Task<PagedResult<SalesReturn>> GetPagedAsync(PagedRequest request, int companyId, int? salesOrderId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@search IS NULL OR sr.ReturnNo LIKE @pattern OR so.InvoiceNo LIKE @pattern)
              AND (@salesOrderId IS NULL OR sr.SalesOrderId = @salesOrderId)
              AND (@companyId = 0 OR sr.CompanyId = @companyId)";

        var countSql = $@"
            SELECT COUNT(1) FROM dbo.SalesReturns sr
            INNER JOIN dbo.SalesOrders so ON so.SalesOrderId = sr.SalesOrderId
            {whereClause}";

        var pagedSql = $@"{HeaderSelect}
            {whereClause}
            ORDER BY sr.ReturnDate DESC, sr.SalesReturnId DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            salesOrderId,
            companyId,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<SalesReturn>(pagedSql, parameters)).ToList();

        return new PagedResult<SalesReturn>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(SalesReturn salesReturn)
    {
        using var connection = _connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            return await InsertReturnAsync(connection, salesReturn, null);
        }

        using var transaction = await dbConnection.BeginTransactionAsync();
        try
        {
            var id = await InsertReturnAsync(connection, salesReturn, transaction);
            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<int> InsertReturnAsync(System.Data.IDbConnection connection, SalesReturn salesReturn, System.Data.IDbTransaction? transaction)
    {
        const string headerSql = @"
            INSERT INTO dbo.SalesReturns (ReturnNo, SalesOrderId, ReturnDate, SubTotal, NetAmount, Reason, Remarks, CompanyId, BusinessUnitId, CreatedAt, CreatedBy)
            OUTPUT INSERTED.SalesReturnId
            VALUES (@ReturnNo, @SalesOrderId, @ReturnDate, @SubTotal, @NetAmount, @Reason, @Remarks, @CompanyId, @BusinessUnitId, @CreatedAt, @CreatedBy)";

        var salesReturnId = await connection.ExecuteScalarAsync<int>(headerSql, salesReturn, transaction);

        const string itemSql = @"
            INSERT INTO dbo.SalesReturnItems (SalesReturnId, SalesOrderItemId, ProductVariantId, Quantity, UnitPrice, LineTotal)
            VALUES (@SalesReturnId, @SalesOrderItemId, @ProductVariantId, @Quantity, @UnitPrice, @LineTotal)";

        foreach (var item in salesReturn.Items)
        {
            item.SalesReturnId = salesReturnId;
            await connection.ExecuteAsync(itemSql, item, transaction);
        }

        return salesReturnId;
    }

    public async Task<string> GenerateNextReturnNoAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT TOP 1 ReturnNo FROM dbo.SalesReturns ORDER BY SalesReturnId DESC";
        var lastReturn = await connection.QuerySingleOrDefaultAsync<string>(sql);

        var nextNumber = 1;
        if (!string.IsNullOrEmpty(lastReturn) && lastReturn.Contains('-'))
        {
            var numericPart = lastReturn.Split('-').Last();
            if (int.TryParse(numericPart, out var parsed))
            {
                nextNumber = parsed + 1;
            }
        }

        return $"SRT-{nextNumber:D6}";
    }
}
