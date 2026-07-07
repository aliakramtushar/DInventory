using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class ExpenseCategoryRepository : IExpenseCategoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ExpenseCategoryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ExpenseCategory?> GetByIdAsync(int expenseCategoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ExpenseCategory>(
            "SELECT * FROM dbo.ExpenseCategories WHERE ExpenseCategoryId = @expenseCategoryId", new { expenseCategoryId });
    }

    public async Task<IEnumerable<ExpenseCategory>> GetAllAsync(int companyId, string? search = null, bool onlyActive = false, int? businessUnitId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.ExpenseCategories
            WHERE (@companyId = 0 OR CompanyId = @companyId)
              AND (@search IS NULL OR ExpenseCategoryName LIKE @pattern)
              AND (@onlyActive = 0 OR IsActive = 1)
              AND (@businessUnitId IS NULL OR BusinessUnitId = @businessUnitId)
            ORDER BY ExpenseCategoryName";
        return await connection.QueryAsync<ExpenseCategory>(sql, new { companyId, search, pattern = $"%{search}%", onlyActive, businessUnitId });
    }

    public async Task<PagedResult<ExpenseCategory>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false, int? businessUnitId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@companyId = 0 OR CompanyId = @companyId)
              AND (@search IS NULL OR ExpenseCategoryName LIKE @pattern)
              AND (@onlyActive = 0 OR IsActive = 1)
              AND (@businessUnitId IS NULL OR BusinessUnitId = @businessUnitId)";

        var countSql = $"SELECT COUNT(1) FROM dbo.ExpenseCategories {whereClause}";
        var pagedSql = $@"
            SELECT * FROM dbo.ExpenseCategories
            {whereClause}
            ORDER BY ExpenseCategoryName, ExpenseCategoryId
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
        var items = (await connection.QueryAsync<ExpenseCategory>(pagedSql, parameters)).ToList();

        return new PagedResult<ExpenseCategory>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(ExpenseCategory expenseCategory)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.ExpenseCategories (ExpenseCategoryName, Description, CompanyId, BusinessUnitId, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.ExpenseCategoryId
            VALUES (@ExpenseCategoryName, @Description, @CompanyId, @BusinessUnitId, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, expenseCategory);
    }

    public async Task<bool> UpdateAsync(ExpenseCategory expenseCategory)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.ExpenseCategories
            SET ExpenseCategoryName = @ExpenseCategoryName, Description = @Description, IsActive = @IsActive,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE ExpenseCategoryId = @ExpenseCategoryId";
        var rows = await connection.ExecuteAsync(sql, expenseCategory);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int expenseCategoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.ExpenseCategories WHERE ExpenseCategoryId = @expenseCategoryId", new { expenseCategoryId });
        return rows > 0;
    }

    public async Task<bool> HasExpensesAsync(int expenseCategoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Expenses WHERE ExpenseCategoryId = @expenseCategoryId", new { expenseCategoryId });
        return count > 0;
    }

    /// <summary>Category names only need to be unique within a company, not globally, since two
    /// unrelated tenants may both want an "Other" category.</summary>
    public async Task<bool> NameExistsAsync(int companyId, string name, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.ExpenseCategories WHERE CompanyId = @companyId AND ExpenseCategoryName = @name AND (@excludeId IS NULL OR ExpenseCategoryId <> @excludeId)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { companyId, name, excludeId });
        return count > 0;
    }
}
