using System.Data.Common;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class SalesOrderRepository : ISalesOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SalesOrderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string HeaderSelect = @"
        SELECT so.SalesOrderId, so.InvoiceNo, so.CustomerId, so.SaleDate, so.SubTotal, so.DiscountAmount,
               so.TaxAmount, so.NetAmount, so.PaymentStatus, so.PaymentMethod, so.Status, so.Remarks, so.CreatedAt, so.CreatedBy,
               c.CustomerName, u.FullName AS CreatedByName
        FROM dbo.SalesOrders so
        LEFT JOIN dbo.Customers c ON c.CustomerId = so.CustomerId
        INNER JOIN dbo.Users u ON u.UserId = so.CreatedBy";

    public async Task<SalesOrder?> GetByIdAsync(int salesOrderId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var order = await connection.QuerySingleOrDefaultAsync<SalesOrder>(
            $"{HeaderSelect} WHERE so.SalesOrderId = @salesOrderId", new { salesOrderId });

        if (order is null)
        {
            return null;
        }

        const string itemsSql = @"
            SELECT soi.SalesOrderItemId, soi.SalesOrderId, soi.ProductVariantId, soi.Quantity, soi.UnitPrice, soi.LineTotal,
                   p.ProductName, p.ProductCode, sz.SizeName, pv.Barcode
            FROM dbo.SalesOrderItems soi
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = soi.ProductVariantId
            INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
            INNER JOIN dbo.Sizes sz ON sz.SizeId = pv.SizeId
            WHERE soi.SalesOrderId = @salesOrderId";

        var items = await connection.QueryAsync<SalesOrderItem>(itemsSql, new { salesOrderId });
        order.Items = items.ToList();

        return order;
    }

    public async Task<PagedResult<SalesOrder>> GetPagedAsync(PagedRequest request, DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@search IS NULL OR so.InvoiceNo LIKE @pattern OR c.CustomerName LIKE @pattern)
              AND (@fromDate IS NULL OR so.SaleDate >= @fromDate)
              AND (@toDate IS NULL OR so.SaleDate < @toDate)";

        var countSql = $@"
            SELECT COUNT(1) FROM dbo.SalesOrders so
            LEFT JOIN dbo.Customers c ON c.CustomerId = so.CustomerId
            {whereClause}";

        var pagedSql = $@"{HeaderSelect}
            {whereClause}
            ORDER BY so.SaleDate DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            fromDate,
            toDate,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<SalesOrder>(pagedSql, parameters)).ToList();

        return new PagedResult<SalesOrder>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(SalesOrder order)
    {
        using var connection = _connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            return await InsertOrderAsync(connection, order, null);
        }

        using var transaction = await dbConnection.BeginTransactionAsync();
        try
        {
            var id = await InsertOrderAsync(connection, order, transaction);
            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<int> InsertOrderAsync(System.Data.IDbConnection connection, SalesOrder order, System.Data.IDbTransaction? transaction)
    {
        const string headerSql = @"
            INSERT INTO dbo.SalesOrders (InvoiceNo, CustomerId, SaleDate, SubTotal, DiscountAmount, TaxAmount,
                                          NetAmount, PaymentStatus, PaymentMethod, Status, Remarks, CreatedAt, CreatedBy)
            OUTPUT INSERTED.SalesOrderId
            VALUES (@InvoiceNo, @CustomerId, @SaleDate, @SubTotal, @DiscountAmount, @TaxAmount,
                    @NetAmount, @PaymentStatus, @PaymentMethod, @Status, @Remarks, @CreatedAt, @CreatedBy)";

        var salesOrderId = await connection.ExecuteScalarAsync<int>(headerSql, order, transaction);

        const string itemSql = @"
            INSERT INTO dbo.SalesOrderItems (SalesOrderId, ProductVariantId, Quantity, UnitPrice, LineTotal)
            VALUES (@SalesOrderId, @ProductVariantId, @Quantity, @UnitPrice, @LineTotal)";

        foreach (var item in order.Items)
        {
            item.SalesOrderId = salesOrderId;
            await connection.ExecuteAsync(itemSql, item, transaction);
        }

        return salesOrderId;
    }

    public async Task<string> GenerateNextInvoiceNoAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT TOP 1 InvoiceNo FROM dbo.SalesOrders ORDER BY SalesOrderId DESC";
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

        return $"INV-{nextNumber:D6}";
    }

    public async Task<decimal> GetSalesTotalAsync(DateTime fromDate, DateTime toDateExclusive)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT ISNULL(SUM(NetAmount), 0) FROM dbo.SalesOrders
            WHERE Status = 'COMPLETED' AND SaleDate >= @fromDate AND SaleDate < @toDateExclusive";
        return await connection.ExecuteScalarAsync<decimal>(sql, new { fromDate, toDateExclusive });
    }

    public async Task<int> GetOrderCountAsync(DateTime fromDate, DateTime toDateExclusive)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT COUNT(1) FROM dbo.SalesOrders
            WHERE Status = 'COMPLETED' AND SaleDate >= @fromDate AND SaleDate < @toDateExclusive";
        return await connection.ExecuteScalarAsync<int>(sql, new { fromDate, toDateExclusive });
    }

    public async Task<IEnumerable<SalesSummaryPoint>> GetSalesTrendAsync(DateTime fromDate, DateTime toDateExclusive, TrendGranularity granularity)
    {
        using var connection = _connectionFactory.CreateConnection();

        string groupExpr = granularity switch
        {
            TrendGranularity.Day => "CONVERT(date, SaleDate)",
            TrendGranularity.Week => "DATEADD(DAY, -(DATEPART(WEEKDAY, SaleDate) - 1), CONVERT(date, SaleDate))",
            TrendGranularity.Month => "DATEFROMPARTS(YEAR(SaleDate), MONTH(SaleDate), 1)",
            TrendGranularity.Year => "DATEFROMPARTS(YEAR(SaleDate), 1, 1)",
            _ => "CONVERT(date, SaleDate)"
        };

        var sql = $@"
            SELECT {groupExpr} AS GroupDate, SUM(NetAmount) AS Total, COUNT(1) AS OrderCount
            FROM dbo.SalesOrders
            WHERE Status = 'COMPLETED' AND SaleDate >= @fromDate AND SaleDate < @toDateExclusive
            GROUP BY {groupExpr}
            ORDER BY GroupDate";

        var rows = await connection.QueryAsync<(DateTime GroupDate, decimal Total, int OrderCount)>(sql, new { fromDate, toDateExclusive });

        return rows.Select(r => new SalesSummaryPoint
        {
            Label = FormatLabel(r.GroupDate, granularity),
            Total = r.Total,
            OrderCount = r.OrderCount
        });
    }

    private static string FormatLabel(DateTime date, TrendGranularity granularity) => granularity switch
    {
        TrendGranularity.Day => date.ToString("MMM dd"),
        TrendGranularity.Week => $"Wk {date:MMM dd}",
        TrendGranularity.Month => date.ToString("MMM yyyy"),
        TrendGranularity.Year => date.ToString("yyyy"),
        _ => date.ToString("MMM dd")
    };

    public async Task<IEnumerable<SalesReportRow>> GetReportRowsAsync(DateTime fromDate, DateTime toDateExclusive)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT so.SaleDate, so.InvoiceNo, c.CustomerName, so.SubTotal, so.DiscountAmount, so.TaxAmount,
                   so.NetAmount, so.Status, u.FullName AS CreatedByName
            FROM dbo.SalesOrders so
            LEFT JOIN dbo.Customers c ON c.CustomerId = so.CustomerId
            INNER JOIN dbo.Users u ON u.UserId = so.CreatedBy
            WHERE so.SaleDate >= @fromDate AND so.SaleDate < @toDateExclusive
            ORDER BY so.SaleDate DESC";
        return await connection.QueryAsync<SalesReportRow>(sql, new { fromDate, toDateExclusive });
    }

    public async Task<IEnumerable<TopProduct>> GetTopProductsAsync(DateTime fromDate, DateTime toDateExclusive, int take = 5)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP (@take) p.ProductName, SUM(soi.Quantity) AS QuantitySold, SUM(soi.LineTotal) AS Revenue
            FROM dbo.SalesOrderItems soi
            INNER JOIN dbo.SalesOrders so ON so.SalesOrderId = soi.SalesOrderId
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = soi.ProductVariantId
            INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
            WHERE so.Status = 'COMPLETED' AND so.SaleDate >= @fromDate AND so.SaleDate < @toDateExclusive
            GROUP BY p.ProductName
            ORDER BY SUM(soi.LineTotal) DESC";
        return await connection.QueryAsync<TopProduct>(sql, new { fromDate, toDateExclusive, take });
    }

    public async Task<bool> CancelAsync(int salesOrderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "UPDATE dbo.SalesOrders SET Status = 'CANCELLED' WHERE SalesOrderId = @salesOrderId";
        var rows = await connection.ExecuteAsync(sql, new { salesOrderId });
        return rows > 0;
    }

    public async Task<decimal> GetProfitTotalAsync(DateTime fromDate, DateTime toDateExclusive)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT ISNULL(SUM(soi.LineTotal - (pr.CostPrice * soi.Quantity)), 0)
            FROM dbo.SalesOrderItems soi
            INNER JOIN dbo.SalesOrders so ON so.SalesOrderId = soi.SalesOrderId
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = soi.ProductVariantId
            INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
            LEFT JOIN dbo.Price pr ON pr.ProductId = p.ProductId AND pr.IsActive = 1
            WHERE so.Status = 'COMPLETED' AND so.SaleDate >= @fromDate AND so.SaleDate < @toDateExclusive";
        return await connection.ExecuteScalarAsync<decimal>(sql, new { fromDate, toDateExclusive });
    }

    public async Task<decimal> GetSalesTotalByMethodAsync(DateTime fromDate, DateTime toDateExclusive, string paymentMethod)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT ISNULL(SUM(NetAmount), 0) FROM dbo.SalesOrders
            WHERE Status = 'COMPLETED' AND PaymentMethod = @paymentMethod
              AND SaleDate >= @fromDate AND SaleDate < @toDateExclusive";
        return await connection.ExecuteScalarAsync<decimal>(sql, new { fromDate, toDateExclusive, paymentMethod });
    }

    public async Task<IEnumerable<ProductProfitRow>> GetProductProfitReportAsync(DateTime fromDate, DateTime toDateExclusive)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT p.ProductName,
                   SUM(soi.Quantity) AS QuantitySold,
                   SUM(soi.LineTotal) AS Revenue,
                   SUM(ISNULL(pr.CostPrice, 0) * soi.Quantity) AS Cost
            FROM dbo.SalesOrderItems soi
            INNER JOIN dbo.SalesOrders so ON so.SalesOrderId = soi.SalesOrderId
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = soi.ProductVariantId
            INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
            LEFT JOIN dbo.Price pr ON pr.ProductId = p.ProductId AND pr.IsActive = 1
            WHERE so.Status = 'COMPLETED' AND so.SaleDate >= @fromDate AND so.SaleDate < @toDateExclusive
            GROUP BY p.ProductName
            ORDER BY SUM(soi.LineTotal) DESC";
        return await connection.QueryAsync<ProductProfitRow>(sql, new { fromDate, toDateExclusive });
    }

    public async Task<IEnumerable<CategorySalesRow>> GetCategorySalesReportAsync(DateTime fromDate, DateTime toDateExclusive)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT c.CategoryName,
                   SUM(soi.Quantity) AS QuantitySold,
                   SUM(soi.LineTotal) AS Revenue
            FROM dbo.SalesOrderItems soi
            INNER JOIN dbo.SalesOrders so ON so.SalesOrderId = soi.SalesOrderId
            INNER JOIN dbo.ProductVariants pv ON pv.ProductVariantId = soi.ProductVariantId
            INNER JOIN dbo.Products p ON p.ProductId = pv.ProductId
            INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            WHERE so.Status = 'COMPLETED' AND so.SaleDate >= @fromDate AND so.SaleDate < @toDateExclusive
            GROUP BY c.CategoryName
            ORDER BY SUM(soi.LineTotal) DESC";
        return await connection.QueryAsync<CategorySalesRow>(sql, new { fromDate, toDateExclusive });
    }

    public async Task<IEnumerable<CustomerReportRow>> GetCustomerSummaryReportAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT c.CustomerId, c.CustomerName, c.Phone,
                   COUNT(so.SalesOrderId) AS OrderCount,
                   SUM(so.NetAmount) AS TotalSpend
            FROM dbo.Customers c
            INNER JOIN dbo.SalesOrders so ON so.CustomerId = c.CustomerId AND so.Status = 'COMPLETED'
            GROUP BY c.CustomerId, c.CustomerName, c.Phone
            ORDER BY SUM(so.NetAmount) DESC";
        return await connection.QueryAsync<CustomerReportRow>(sql);
    }
}
