using DInventory.Application.Common.Interfaces;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PasswordResetTokenRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateAsync(PasswordResetToken token)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.PasswordResetTokens (UserId, TokenHash, ExpiresAt, IsUsed, CreatedAt)
            OUTPUT INSERTED.ResetTokenId
            VALUES (@UserId, @TokenHash, @ExpiresAt, @IsUsed, @CreatedAt)";
        return await connection.ExecuteScalarAsync<int>(sql, token);
    }

    public async Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM dbo.PasswordResetTokens WHERE TokenHash = @tokenHash";
        return await connection.QuerySingleOrDefaultAsync<PasswordResetToken>(sql, new { tokenHash });
    }

    public async Task<bool> MarkUsedAsync(int resetTokenId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "UPDATE dbo.PasswordResetTokens SET IsUsed = 1 WHERE ResetTokenId = @resetTokenId";
        var rows = await connection.ExecuteAsync(sql, new { resetTokenId });
        return rows > 0;
    }

    public async Task InvalidateAllForUserAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "UPDATE dbo.PasswordResetTokens SET IsUsed = 1 WHERE UserId = @userId AND IsUsed = 0";
        await connection.ExecuteAsync(sql, new { userId });
    }
}
