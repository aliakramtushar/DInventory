using System.Data.Common;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class PurchaseRepository : IPurchaseRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PurchaseRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string HeaderSelect = @"
        SELECT p.PurchaseId, p.PurchaseInvoiceNo, p.SupplierId, p.PurchaseDate, p.TotalAmount, p.PaidAmount,
               p.Remarks, p.CompanyId, p.BusinessUnitId, p.CreatedAt, p.CreatedBy,
               s.SupplierName, u.FullName AS CreatedByName, comp.CompanyName,
               ISNULL(pret.ReturnedAmount, 0) AS ReturnedAmount
        FROM dbo.Purchases p
        INNER JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
        INNER JOIN dbo.Users u ON u.UserId = p.CreatedBy
        LEFT JOIN dbo.Companies comp ON comp.CompanyId = p.CompanyId
        OUTER APPLY (
            SELECT SUM(r.TotalAmount) AS ReturnedAmount FROM dbo.PurchaseReturns r WHERE r.PurchaseId = p.PurchaseId
        ) pret";

    public async Task<Purchase?> GetByIdAsync(int purchaseId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var purchase = await connection.QuerySingleOrDefaultAsync<Purchase>(
            $"{HeaderSelect} WHERE p.PurchaseId = @purchaseId", new { purchaseId });

        if (purchase is null)
        {
            return null;
        }

        const string itemsSql = @"
            SELECT pi.PurchaseItemId, pi.PurchaseId, pi.ProductVariantId, pi.Quantity, pi.BuyingPrice, pi.LineTotal,
                   pr.ProductName, pr.ProductCode, sz.SizeName, co.ColorName, pv.Barcode,
                   ISNULL(ret.ReturnedQuantity, 0) AS ReturnedQuantity
            FROM dbo.PurchaseItems pi
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = pi.ProductVariantId
            INNER JOIN dbo.Products pr ON pr.ProductId = pv.ProductId
            INNER JOIN dbo.Sizes sz ON sz.SizeId = pv.SizeId
            LEFT JOIN dbo.Colors co ON co.ColorId = pv.ColorId
            OUTER APPLY (
                SELECT SUM(pri.Quantity) AS ReturnedQuantity FROM dbo.PurchaseReturnItems pri WHERE pri.PurchaseItemId = pi.PurchaseItemId
            ) ret
            WHERE pi.PurchaseId = @purchaseId";

        var items = await connection.QueryAsync<PurchaseItem>(itemsSql, new { purchaseId });
        purchase.Items = items.ToList();

        return purchase;
    }

    public async Task<PagedResult<Purchase>> GetPagedAsync(PagedRequest request, int companyId, int? businessUnitId = null, int? supplierId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@companyId = 0 OR p.CompanyId = @companyId)
              AND (@businessUnitId IS NULL OR p.BusinessUnitId = @businessUnitId)
              AND (@search IS NULL OR p.PurchaseInvoiceNo LIKE @pattern OR s.SupplierName LIKE @pattern)
              AND (@supplierId IS NULL OR p.SupplierId = @supplierId)";

        var countSql = $@"
            SELECT COUNT(1) FROM dbo.Purchases p
            INNER JOIN dbo.Suppliers s ON s.SupplierId = p.SupplierId
            {whereClause}";

        var pagedSql = $@"{HeaderSelect}
            {whereClause}
            ORDER BY p.PurchaseDate DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            companyId,
            businessUnitId,
            search = request.Search,
            pattern = $"%{request.Search}%",
            supplierId,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Purchase>(pagedSql, parameters)).ToList();

        return new PagedResult<Purchase>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<IEnumerable<Purchase>> GetForSupplierAsync(int supplierId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $"{HeaderSelect} WHERE p.SupplierId = @supplierId ORDER BY p.PurchaseDate DESC";
        return await connection.QueryAsync<Purchase>(sql, new { supplierId });
    }

    public async Task<int> CreateAsync(Purchase purchase)
    {
        using var connection = _connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            return await InsertPurchaseAsync(connection, purchase, null);
        }

        using var transaction = await dbConnection.BeginTransactionAsync();
        try
        {
            var id = await InsertPurchaseAsync(connection, purchase, transaction);
            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<int> InsertPurchaseAsync(System.Data.IDbConnection connection, Purchase purchase, System.Data.IDbTransaction? transaction)
    {
        const string headerSql = @"
            INSERT INTO dbo.Purchases (PurchaseInvoiceNo, SupplierId, PurchaseDate, TotalAmount, PaidAmount, Remarks, CompanyId, BusinessUnitId, CreatedAt, CreatedBy)
            OUTPUT INSERTED.PurchaseId
            VALUES (@PurchaseInvoiceNo, @SupplierId, @PurchaseDate, @TotalAmount, @PaidAmount, @Remarks, @CompanyId, @BusinessUnitId, @CreatedAt, @CreatedBy)";

        var purchaseId = await connection.ExecuteScalarAsync<int>(headerSql, purchase, transaction);

        const string itemSql = @"
            INSERT INTO dbo.PurchaseItems (PurchaseId, ProductVariantId, Quantity, BuyingPrice, LineTotal)
            VALUES (@PurchaseId, @ProductVariantId, @Quantity, @BuyingPrice, @LineTotal)";

        foreach (var item in purchase.Items)
        {
            item.PurchaseId = purchaseId;
            await connection.ExecuteAsync(itemSql, item, transaction);
        }

        return purchaseId;
    }

    public async Task<string> GenerateNextInvoiceNoAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT TOP 1 PurchaseInvoiceNo FROM dbo.Purchases ORDER BY PurchaseId DESC";
        var lastInvoice = await connection.QuerySingleOrDefaultAsync<string>(sql);

        var nextNumber = 1;
        if (!string.IsNullOrEmpty(lastInvoice) && lastInvoice.Contains('-'))
        {
            var numericPart = lastInvoice.Split('-').Last();
            if (int.TryParse(numericPart, out var parsed))
            {
                nextNumber = parsed + 1;
            }
        }

        return $"PUR-{nextNumber:D6}";
    }
}
