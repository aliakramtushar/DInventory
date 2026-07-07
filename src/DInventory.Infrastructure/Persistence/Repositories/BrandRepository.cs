using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class BrandRepository : IBrandRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public BrandRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Brand?> GetByIdAsync(int brandId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Brand>("SELECT * FROM dbo.Brands WHERE BrandId = @brandId", new { brandId });
    }

    public async Task<IEnumerable<Brand>> GetAllAsync(int companyId, bool onlyActive = false, int? businessUnitId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.Brands
            WHERE (@companyId = 0 OR CompanyId = @companyId)
              AND (@onlyActive = 0 OR IsActive = 1)
              AND (@businessUnitId IS NULL OR BusinessUnitId = @businessUnitId)
            ORDER BY BrandName";
        return await connection.QueryAsync<Brand>(sql, new { companyId, onlyActive, businessUnitId });
    }

    public async Task<PagedResult<Brand>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false, int? businessUnitId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@companyId = 0 OR CompanyId = @companyId)
              AND (@search IS NULL OR BrandName LIKE @pattern)
              AND (@onlyActive = 0 OR IsActive = 1)
              AND (@businessUnitId IS NULL OR BusinessUnitId = @businessUnitId)";

        var countSql = $"SELECT COUNT(1) FROM dbo.Brands {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.Brands
            {whereClause}
            ORDER BY BrandName, BrandId
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            companyId,
            search = request.Search,
            pattern = $"%{request.Search}%",
            onlyActive,
            businessUnitId,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Brand>(pagedSql, parameters)).ToList();

        return new PagedResult<Brand>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(Brand brand)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Brands (BrandName, Description, CompanyId, BusinessUnitId, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.BrandId
            VALUES (@BrandName, @Description, @CompanyId, @BusinessUnitId, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, brand);
    }

    public async Task<bool> UpdateAsync(Brand brand)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Brands
            SET BrandName = @BrandName, Description = @Description, IsActive = @IsActive,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE BrandId = @BrandId";
        var rows = await connection.ExecuteAsync(sql, brand);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int brandId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Brands WHERE BrandId = @brandId", new { brandId });
        return rows > 0;
    }

    public async Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Brands WHERE CompanyId = @companyId AND BrandName = @name AND (@excludeId IS NULL OR BrandId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { companyId, name, excludeId });
        return count > 0;
    }

    public async Task<bool> HasProductsAsync(int brandId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Products WHERE BrandId = @brandId", new { brandId });
        return count > 0;
    }
}
