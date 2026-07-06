using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string StatsSelect = @"
        SELECT c.*,
               ISNULL(so.OrderCount, 0) AS OrderCount,
               ISNULL(so.TotalSpend, 0) AS TotalSpend,
               ISNULL(lt.Balance, 0) AS LoyaltyPointsBalance
        FROM dbo.Customers c
        OUTER APPLY (
            SELECT COUNT(1) AS OrderCount, SUM(s.NetAmount) AS TotalSpend
            FROM dbo.SalesOrders s
            WHERE s.CustomerId = c.CustomerId AND s.Status = 'COMPLETED'
        ) so
        OUTER APPLY (
            SELECT SUM(t.Points) AS Balance FROM dbo.LoyaltyTransactions t WHERE t.CustomerId = c.CustomerId
        ) lt";

    public async Task<Customer?> GetByIdAsync(int customerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Customer>("SELECT * FROM dbo.Customers WHERE CustomerId = @customerId", new { customerId });
    }

    public async Task<Customer?> GetByIdWithStatsAsync(int customerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Customer>($"{StatsSelect} WHERE c.CustomerId = @customerId", new { customerId });
    }

    public async Task<IEnumerable<Customer>> GetAllAsync(string? search = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.Customers
            WHERE IsActive = 1 AND (@search IS NULL OR CustomerName LIKE @pattern OR Phone LIKE @pattern)
            ORDER BY CustomerName";
        return await connection.QueryAsync<Customer>(sql, new { search, pattern = $"%{search}%" });
    }

    public async Task<PagedResult<Customer>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@companyId = 0 OR c.CompanyId = @companyId)
              AND (@search IS NULL OR c.CustomerName LIKE @pattern OR c.Phone LIKE @pattern OR c.Email LIKE @pattern)
              AND (@onlyActive = 0 OR c.IsActive = 1)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Customers c {whereClause}";
        var pagedSql = $@"{StatsSelect}
            {whereClause}
            ORDER BY c.CustomerName, c.CustomerId
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
        var items = (await connection.QueryAsync<Customer>(pagedSql, parameters)).ToList();

        return new PagedResult<Customer>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(Customer customer)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Customers (CustomerName, Phone, Email, Address, CompanyId, BusinessUnitId, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.CustomerId
            VALUES (@CustomerName, @Phone, @Email, @Address, @CompanyId, @BusinessUnitId, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, customer);
    }

    public async Task<bool> UpdateAsync(Customer customer)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Customers
            SET CustomerName = @CustomerName, Phone = @Phone, Email = @Email, Address = @Address, IsActive = @IsActive,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE CustomerId = @CustomerId";
        var rows = await connection.ExecuteAsync(sql, customer);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int customerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Customers WHERE CustomerId = @customerId", new { customerId });
        return rows > 0;
    }

    public async Task<bool> HasSalesAsync(int customerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.SalesOrders WHERE CustomerId = @customerId", new { customerId });
        return count > 0;
    }

    public async Task<int> GetTotalCountAsync(int companyId = 0)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Customers WHERE IsActive = 1 AND (@companyId = 0 OR CompanyId = @companyId)";
        return await connection.ExecuteScalarAsync<int>(sql, new { companyId });
    }
}
