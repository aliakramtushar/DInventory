using DInventory.Application.Common.Interfaces;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class PriceRepository : IPriceRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PriceRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Price?> GetActivePriceAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM dbo.Price WHERE ProductId = @productId AND IsActive = 1";
        return await connection.QuerySingleOrDefaultAsync<Price>(sql, new { productId });
    }

    public async Task<IEnumerable<Price>> GetHistoryAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM dbo.Price WHERE ProductId = @productId ORDER BY EffectiveFrom DESC";
        return await connection.QueryAsync<Price>(sql, new { productId });
    }

    public async Task<int> CreateAsync(Price price)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Price (ProductId, CostPrice, SellingPrice, EffectiveFrom, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.PriceId
            VALUES (@ProductId, @CostPrice, @SellingPrice, @EffectiveFrom, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, price);
    }

    public async Task DeactivateAllForProductAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("UPDATE dbo.Price SET IsActive = 0 WHERE ProductId = @productId", new { productId });
    }
}
