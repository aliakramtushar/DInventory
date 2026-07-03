using DInventory.Application.Common.Interfaces;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RefreshTokenRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateAsync(RefreshToken token)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.RefreshTokens (UserId, TokenHash, ExpiresAt, CreatedAt, CreatedByIp)
            OUTPUT INSERTED.RefreshTokenId
            VALUES (@UserId, @TokenHash, @ExpiresAt, @CreatedAt, @CreatedByIp)";
        return await connection.ExecuteScalarAsync<int>(sql, token);
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM dbo.RefreshTokens WHERE TokenHash = @tokenHash";
        return await connection.QuerySingleOrDefaultAsync<RefreshToken>(sql, new { tokenHash });
    }

    public async Task<bool> RevokeAsync(string tokenHash, string? revokedByIp, string? replacedByTokenHash = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.RefreshTokens
            SET RevokedAt = SYSUTCDATETIME(), RevokedByIp = @revokedByIp, ReplacedByTokenHash = @replacedByTokenHash
            WHERE TokenHash = @tokenHash AND RevokedAt IS NULL";
        var rows = await connection.ExecuteAsync(sql, new { tokenHash, revokedByIp, replacedByTokenHash });
        return rows > 0;
    }

    public async Task<int> RevokeAllForUserAsync(int userId, string? revokedByIp = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.RefreshTokens
            SET RevokedAt = SYSUTCDATETIME(), RevokedByIp = @revokedByIp
            WHERE UserId = @userId AND RevokedAt IS NULL";
        return await connection.ExecuteAsync(sql, new { userId, revokedByIp });
    }

    public async Task<IEnumerable<RefreshToken>> GetActiveTokensForUserAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.RefreshTokens
            WHERE UserId = @userId AND RevokedAt IS NULL AND ExpiresAt > SYSUTCDATETIME()";
        return await connection.QueryAsync<RefreshToken>(sql, new { userId });
    }
}
