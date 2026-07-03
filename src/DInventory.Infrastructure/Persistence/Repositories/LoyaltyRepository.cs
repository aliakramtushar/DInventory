using DInventory.Application.Common.Interfaces;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class LoyaltyRepository : ILoyaltyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LoyaltyRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<LoyaltySettings> GetSettingsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var settings = await connection.QuerySingleOrDefaultAsync<LoyaltySettings>(
            "SELECT TOP (1) * FROM dbo.LoyaltySettings ORDER BY LoyaltySettingsId");

        // Database.sql always seeds exactly one row, but fall back to an all-zero (effectively
        // off) settings object rather than throwing if that seed somehow hasn't run yet.
        return settings ?? new LoyaltySettings();
    }

    public async Task<bool> UpdateSettingsAsync(LoyaltySettings settings)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.LoyaltySettings
            SET IsEnabled = @IsEnabled, PointsPerAmountSpent = @PointsPerAmountSpent, PointValueOnRedeem = @PointValueOnRedeem,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE LoyaltySettingsId = @LoyaltySettingsId";
        var rows = await connection.ExecuteAsync(sql, settings);
        return rows > 0;
    }

    public async Task<int> GetBalanceAsync(int customerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT ISNULL(SUM(Points), 0) FROM dbo.LoyaltyTransactions WHERE CustomerId = @customerId";
        return await connection.ExecuteScalarAsync<int>(sql, new { customerId });
    }

    public async Task<IEnumerable<LoyaltyTransaction>> GetHistoryAsync(int customerId, int take = 100)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP (@take) t.LoyaltyTransactionId, t.CustomerId, t.TransactionType, t.Points,
                   t.ReferenceType, t.ReferenceId, t.Remarks, t.CreatedAt, t.CreatedBy,
                   u.FullName AS CreatedByName
            FROM dbo.LoyaltyTransactions t
            LEFT JOIN dbo.Users u ON u.UserId = t.CreatedBy
            WHERE t.CustomerId = @customerId
            ORDER BY t.CreatedAt DESC, t.LoyaltyTransactionId DESC";
        return await connection.QueryAsync<LoyaltyTransaction>(sql, new { customerId, take });
    }

    public async Task<int> CreateTransactionAsync(LoyaltyTransaction transaction)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.LoyaltyTransactions (CustomerId, TransactionType, Points, ReferenceType, ReferenceId, Remarks, CreatedAt, CreatedBy)
            OUTPUT INSERTED.LoyaltyTransactionId
            VALUES (@CustomerId, @TransactionType, @Points, @ReferenceType, @ReferenceId, @Remarks, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, transaction);
    }
}
