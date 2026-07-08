using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CompanyRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Company?> GetByIdAsync(int companyId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Company>("SELECT * FROM dbo.Companies WHERE CompanyId = @companyId", new { companyId });
    }

    public async Task<IEnumerable<Company>> GetAllAsync(string? search = null, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.Companies
            WHERE (@search IS NULL OR CompanyName LIKE @pattern)
              AND (@onlyActive = 0 OR IsActive = 1)
            ORDER BY CompanyName";
        return await connection.QueryAsync<Company>(sql, new { search, pattern = $"%{search}%", onlyActive });
    }

    public async Task<PagedResult<Company>> GetPagedAsync(PagedRequest request, bool onlyActive = false)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@search IS NULL OR CompanyName LIKE @pattern)
              AND (@onlyActive = 0 OR IsActive = 1)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Companies {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.Companies
            {whereClause}
            ORDER BY CompanyId
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            onlyActive,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Company>(pagedSql, parameters)).ToList();

        return new PagedResult<Company>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(Company company)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Companies (CompanyName, ShortName, Phone, Email, Address, HasECommerce, Language, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.CompanyId
            VALUES (@CompanyName, @ShortName, @Phone, @Email, @Address, @HasECommerce, @Language, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, company);
    }

    public async Task<bool> UpdateAsync(Company company)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Companies
            SET CompanyName = @CompanyName, ShortName = @ShortName, Phone = @Phone, Email = @Email, Address = @Address,
                HasECommerce = @HasECommerce, Language = @Language, IsActive = @IsActive,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE CompanyId = @CompanyId";
        var rows = await connection.ExecuteAsync(sql, company);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int companyId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Companies WHERE CompanyId = @companyId", new { companyId });
        return rows > 0;
    }

    /// <summary>Checked before delete - a company with any users or business units (or catalog/sales
    /// data) attached should be deactivated instead of deleted.</summary>
    public async Task<bool> HasDependentDataAsync(int companyId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT
                (SELECT COUNT(1) FROM dbo.Users WHERE CompanyId = @companyId) +
                (SELECT COUNT(1) FROM dbo.BusinessUnits WHERE CompanyId = @companyId) +
                (SELECT COUNT(1) FROM dbo.Products WHERE CompanyId = @companyId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { companyId });
        return count > 0;
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Companies WHERE CompanyName = @name AND (@excludeId IS NULL OR CompanyId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { name, excludeId });
        return count > 0;
    }

    public async Task<bool> ShortNameExistsAsync(string shortName, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Companies WHERE ShortName = @shortName AND (@excludeId IS NULL OR CompanyId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { shortName, excludeId });
        return count > 0;
    }
}
