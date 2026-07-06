using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SupplierRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Supplier?> GetByIdAsync(int supplierId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Supplier>("SELECT * FROM dbo.Suppliers WHERE SupplierId = @supplierId", new { supplierId });
    }

    public async Task<Supplier?> GetByIdWithDueAsync(int supplierId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT s.*,
                   ISNULL(pu.TotalPurchased, 0) AS TotalPurchased,
                   ISNULL(pu.TotalPaid, 0) AS TotalPaid,
                   ISNULL(pu.TotalPurchased, 0) - ISNULL(pu.TotalPaid, 0) AS DueAmount
            FROM dbo.Suppliers s
            OUTER APPLY (
                SELECT SUM(p.TotalAmount) AS TotalPurchased, SUM(p.PaidAmount) AS TotalPaid
                FROM dbo.Purchases p
                WHERE p.SupplierId = s.SupplierId
            ) pu
            WHERE s.SupplierId = @supplierId";
        return await connection.QuerySingleOrDefaultAsync<Supplier>(sql, new { supplierId });
    }

    public async Task<IEnumerable<Supplier>> GetAllAsync(bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.Suppliers
            WHERE (@onlyActive = 0 OR IsActive = 1)
            ORDER BY SupplierName";
        return await connection.QueryAsync<Supplier>(sql, new { onlyActive });
    }

    public async Task<PagedResult<Supplier>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@companyId = 0 OR CompanyId = @companyId)
              AND (@search IS NULL OR SupplierName LIKE @pattern OR Phone LIKE @pattern)
              AND (@onlyActive = 0 OR IsActive = 1)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Suppliers {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.Suppliers
            {whereClause}
            ORDER BY SupplierName
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            companyId,
            search = request.Search,
            pattern = $"%{request.Search}%",
            onlyActive,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Supplier>(pagedSql, parameters)).ToList();

        return new PagedResult<Supplier>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(Supplier supplier)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Suppliers (SupplierName, Phone, Address, CompanyId, BusinessUnitId, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.SupplierId
            VALUES (@SupplierName, @Phone, @Address, @CompanyId, @BusinessUnitId, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, supplier);
    }

    public async Task<bool> UpdateAsync(Supplier supplier)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Suppliers
            SET SupplierName = @SupplierName, Phone = @Phone, Address = @Address, IsActive = @IsActive,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE SupplierId = @SupplierId";
        var rows = await connection.ExecuteAsync(sql, supplier);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int supplierId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Suppliers WHERE SupplierId = @supplierId", new { supplierId });
        return rows > 0;
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Suppliers WHERE SupplierName = @name AND (@excludeId IS NULL OR SupplierId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { name, excludeId });
        return count > 0;
    }

    public async Task<bool> HasPurchasesAsync(int supplierId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Purchases WHERE SupplierId = @supplierId", new { supplierId });
        return count > 0;
    }
}
