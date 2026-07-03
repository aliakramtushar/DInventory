using DInventory.Application.Common.Interfaces;
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

    public async Task<Customer?> GetByIdAsync(int customerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Customer>("SELECT * FROM dbo.Customers WHERE CustomerId = @customerId", new { customerId });
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

    public async Task<int> CreateAsync(Customer customer)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Customers (CustomerName, Phone, Email, Address, IsActive, CreatedAt)
            OUTPUT INSERTED.CustomerId
            VALUES (@CustomerName, @Phone, @Email, @Address, @IsActive, @CreatedAt)";
        return await connection.ExecuteScalarAsync<int>(sql, customer);
    }

    public async Task<int> GetTotalCountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Customers WHERE IsActive = 1");
    }
}
