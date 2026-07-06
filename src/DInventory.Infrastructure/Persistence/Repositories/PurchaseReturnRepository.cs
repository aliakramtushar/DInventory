using System.Data.Common;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class PurchaseReturnRepository : IPurchaseReturnRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PurchaseReturnRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string HeaderSelect = @"
        SELECT pr.PurchaseReturnId, pr.ReturnNo, pr.PurchaseId, pr.ReturnDate, pr.TotalAmount,
               pr.Reason, pr.Remarks, pr.CreatedAt, pr.CreatedBy,
               p.PurchaseInvoiceNo, s.SupplierName, u.FullName AS CreatedByName
        FROM dbo.PurchaseReturns pr
        INNER JOIN dbo.Purchases p ON p.PurchaseId = pr.PurchaseId
        INNER JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
        INNER JOIN dbo.Users u ON u.UserId = pr.CreatedBy";

    public async Task<PurchaseReturn?> GetByIdAsync(int purchaseReturnId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var purchaseReturn = await connection.QuerySingleOrDefaultAsync<PurchaseReturn>(
            $"{HeaderSelect} WHERE pr.PurchaseReturnId = @purchaseReturnId", new { purchaseReturnId });

        if (purchaseReturn is null)
        {
            return null;
        }

        const string itemsSql = @"
            SELECT pri.PurchaseReturnItemId, pri.PurchaseReturnId, pri.PurchaseItemId, pri.ProductVariantId,
                   pri.Quantity, pri.UnitPrice, pri.LineTotal,
                   pr2.ProductName, pr2.ProductCode, sz.SizeName, co.ColorName, pv.Barcode
            FROM dbo.PurchaseReturnItems pri
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = pri.ProductVariantId
            INNER JOIN dbo.Products pr2 ON pr2.ProductId = pv.ProductId
            INNER JOIN dbo.Sizes sz ON sz.SizeId = pv.SizeId
            LEFT JOIN dbo.Colors co ON co.ColorId = pv.ColorId
            WHERE pri.PurchaseReturnId = @purchaseReturnId";

        var items = await connection.QueryAsync<PurchaseReturnItem>(itemsSql, new { purchaseReturnId });
        purchaseReturn.Items = items.ToList();

        return purchaseReturn;
    }

    public async Task<PagedResult<PurchaseReturn>> GetPagedAsync(PagedRequest request, int companyId, int? purchaseId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@search IS NULL OR pr.ReturnNo LIKE @pattern OR p.PurchaseInvoiceNo LIKE @pattern)
              AND (@purchaseId IS NULL OR pr.PurchaseId = @purchaseId)
              AND (@companyId = 0 OR pr.CompanyId = @companyId)";

        var countSql = $@"
            SELECT COUNT(1) FROM dbo.PurchaseReturns pr
            INNER JOIN dbo.Purchases p ON p.PurchaseId = pr.PurchaseId
            {whereClause}";

        var pagedSql = $@"{HeaderSelect}
            {whereClause}
            ORDER BY pr.ReturnDate DESC, pr.PurchaseReturnId DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            purchaseId,
            companyId,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<PurchaseReturn>(pagedSql, parameters)).ToList();

        return new PagedResult<PurchaseReturn>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(PurchaseReturn purchaseReturn)
    {
        using var connection = _connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            return await InsertReturnAsync(connection, purchaseReturn, null);
        }

        using var transaction = await dbConnection.BeginTransactionAsync();
        try
        {
            var id = await InsertReturnAsync(connection, purchaseReturn, transaction);
            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<int> InsertReturnAsync(System.Data.IDbConnection connection, PurchaseReturn purchaseReturn, System.Data.IDbTransaction? transaction)
    {
        const string headerSql = @"
            INSERT INTO dbo.PurchaseReturns (ReturnNo, PurchaseId, ReturnDate, TotalAmount, Reason, Remarks, CompanyId, BusinessUnitId, CreatedAt, CreatedBy)
            OUTPUT INSERTED.PurchaseReturnId
            VALUES (@ReturnNo, @PurchaseId, @ReturnDate, @TotalAmount, @Reason, @Remarks, @CompanyId, @BusinessUnitId, @CreatedAt, @CreatedBy)";

        var purchaseReturnId = await connection.ExecuteScalarAsync<int>(headerSql, purchaseReturn, transaction);

        const string itemSql = @"
            INSERT INTO dbo.PurchaseReturnItems (PurchaseReturnId, PurchaseItemId, ProductVariantId, Quantity, UnitPrice, LineTotal)
            VALUES (@PurchaseReturnId, @PurchaseItemId, @ProductVariantId, @Quantity, @UnitPrice, @LineTotal)";

        foreach (var item in purchaseReturn.Items)
        {
            item.PurchaseReturnId = purchaseReturnId;
            await connection.ExecuteAsync(itemSql, item, transaction);
        }

        return purchaseReturnId;
    }

    public async Task<string> GenerateNextReturnNoAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT TOP 1 ReturnNo FROM dbo.PurchaseReturns ORDER BY PurchaseReturnId DESC";
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

        return $"PRT-{nextNumber:D6}";
    }
}
