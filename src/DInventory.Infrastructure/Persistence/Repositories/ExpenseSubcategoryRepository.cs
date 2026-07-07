using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class ExpenseSubcategoryRepository : IExpenseSubcategoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ExpenseSubcategoryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectBase = @"
        SELECT s.ExpenseSubcategoryId, s.ExpenseCategoryId, s.ExpenseSubcategoryName, s.Description, s.IsActive,
               s.CompanyId, s.BusinessUnitId, s.CreatedAt, s.UpdatedAt, s.CreatedBy, s.UpdatedBy, c.ExpenseCategoryName
        FROM dbo.ExpenseSubcategories s
        INNER JOIN dbo.ExpenseCategories c ON c.ExpenseCategoryId = s.ExpenseCategoryId";

    public async Task<ExpenseSubcategory?> GetByIdAsync(int expenseSubcategoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ExpenseSubcategory>($"{SelectBase} WHERE s.ExpenseSubcategoryId = @expenseSubcategoryId", new { expenseSubcategoryId });
    }

    public async Task<IEnumerable<ExpenseSubcategory>> GetAllAsync(int companyId, int? expenseCategoryId = null, string? search = null, bool onlyActive = false, int? businessUnitId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $@"{SelectBase}
            WHERE (@companyId = 0 OR s.CompanyId = @companyId)
              AND (@expenseCategoryId IS NULL OR s.ExpenseCategoryId = @expenseCategoryId)
              AND (@search IS NULL OR s.ExpenseSubcategoryName LIKE @pattern)
              AND (@onlyActive = 0 OR s.IsActive = 1)
              AND (@businessUnitId IS NULL OR s.BusinessUnitId = @businessUnitId)
            ORDER BY s.ExpenseSubcategoryName";
        return await connection.QueryAsync<ExpenseSubcategory>(sql, new { companyId, expenseCategoryId, search, pattern = $"%{search}%", onlyActive, businessUnitId });
    }

    public async Task<PagedResult<ExpenseSubcategory>> GetPagedAsync(PagedRequest request, int companyId, int? expenseCategoryId = null, bool onlyActive = false, int? businessUnitId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@companyId = 0 OR s.CompanyId = @companyId)
              AND (@expenseCategoryId IS NULL OR s.ExpenseCategoryId = @expenseCategoryId)
              AND (@search IS NULL OR s.ExpenseSubcategoryName LIKE @pattern)
              AND (@onlyActive = 0 OR s.IsActive = 1)
              AND (@businessUnitId IS NULL OR s.BusinessUnitId = @businessUnitId)";

        var countSql = $@"
            SELECT COUNT(1)
            FROM dbo.ExpenseSubcategories s
            INNER JOIN dbo.ExpenseCategories c ON c.ExpenseCategoryId = s.ExpenseCategoryId
            {whereClause}";

        var pagedSql = $@"{SelectBase}
            {whereClause}
            ORDER BY s.ExpenseSubcategoryName, s.ExpenseSubcategoryId
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            companyId,
            expenseCategoryId,
            search = request.Search,
            pattern = $"%{request.Search}%",
            onlyActive,
            businessUnitId,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<ExpenseSubcategory>(pagedSql, parameters)).ToList();

        return new PagedResult<ExpenseSubcategory>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(ExpenseSubcategory expenseSubcategory)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.ExpenseSubcategories (ExpenseCategoryId, ExpenseSubcategoryName, Description, CompanyId, BusinessUnitId, IsActive, CreatedAt, CreatedBy)
            OUTPUT INSERTED.ExpenseSubcategoryId
            VALUES (@ExpenseCategoryId, @ExpenseSubcategoryName, @Description, @CompanyId, @BusinessUnitId, @IsActive, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, expenseSubcategory);
    }

    public async Task<bool> UpdateAsync(ExpenseSubcategory expenseSubcategory)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.ExpenseSubcategories
            SET ExpenseCategoryId = @ExpenseCategoryId, ExpenseSubcategoryName = @ExpenseSubcategoryName, Description = @Description,
                IsActive = @IsActive, UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE ExpenseSubcategoryId = @ExpenseSubcategoryId";
        var rows = await connection.ExecuteAsync(sql, expenseSubcategory);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int expenseSubcategoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.ExpenseSubcategories WHERE ExpenseSubcategoryId = @expenseSubcategoryId", new { expenseSubcategoryId });
        return rows > 0;
    }

    public async Task<bool> HasExpensesAsync(int expenseSubcategoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Expenses WHERE ExpenseSubcategoryId = @expenseSubcategoryId", new { expenseSubcategoryId });
        return count > 0;
    }
}
