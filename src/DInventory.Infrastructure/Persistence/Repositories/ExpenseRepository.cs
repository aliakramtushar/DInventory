using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class ExpenseRepository : IExpenseRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ExpenseRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectBase = @"
        SELECT e.ExpenseId, e.ExpenseDate, e.ExpenseCategoryId, e.ExpenseSubcategoryId, e.Amount,
               e.CompanyId, e.BusinessUnitId, e.Remarks, e.CreatedAt, e.CreatedBy,
               u.FullName AS CreatedByName, ec.ExpenseCategoryName, esc.ExpenseSubcategoryName
        FROM dbo.Expenses e
        INNER JOIN dbo.ExpenseCategories ec ON ec.ExpenseCategoryId = e.ExpenseCategoryId
        LEFT JOIN dbo.ExpenseSubcategories esc ON esc.ExpenseSubcategoryId = e.ExpenseSubcategoryId
        LEFT JOIN dbo.Users u ON u.UserId = e.CreatedBy";

    public async Task<Expense?> GetByIdAsync(int expenseId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Expense>(
            $"{SelectBase} WHERE e.ExpenseId = @expenseId", new { expenseId });
    }

    public async Task<PagedResult<Expense>> GetPagedAsync(PagedRequest request, DateTime? fromDate = null, DateTime? toDate = null, int? expenseCategoryId = null, int companyId = 0)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClause = @"
            WHERE (@search IS NULL OR e.Remarks LIKE @pattern OR ec.ExpenseCategoryName LIKE @pattern)
              AND (@fromDate IS NULL OR e.ExpenseDate >= @fromDate)
              AND (@toDate IS NULL OR e.ExpenseDate < @toDate)
              AND (@expenseCategoryId IS NULL OR e.ExpenseCategoryId = @expenseCategoryId)
              AND (@companyId = 0 OR e.CompanyId = @companyId)";

        var countSql = $@"
            SELECT COUNT(1) FROM dbo.Expenses e
            INNER JOIN dbo.ExpenseCategories ec ON ec.ExpenseCategoryId = e.ExpenseCategoryId
            {whereClause}";

        var pagedSql = $@"{SelectBase}
            {whereClause}
            ORDER BY e.ExpenseDate DESC, e.ExpenseId DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var parameters = new
        {
            search = request.Search,
            pattern = $"%{request.Search}%",
            fromDate,
            toDate,
            expenseCategoryId,
            companyId,
            offset = (request.PageNumber - 1) * request.PageSize,
            pageSize = request.PageSize
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await connection.QueryAsync<Expense>(pagedSql, parameters)).ToList();

        return new PagedResult<Expense>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CreateAsync(Expense expense)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.Expenses (ExpenseDate, ExpenseCategoryId, ExpenseSubcategoryId, Amount, Remarks, CompanyId, BusinessUnitId, CreatedAt, CreatedBy)
            OUTPUT INSERTED.ExpenseId
            VALUES (@ExpenseDate, @ExpenseCategoryId, @ExpenseSubcategoryId, @Amount, @Remarks, @CompanyId, @BusinessUnitId, @CreatedAt, @CreatedBy)";
        return await connection.ExecuteScalarAsync<int>(sql, expense);
    }

    public async Task<bool> UpdateAsync(Expense expense)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.Expenses
            SET ExpenseDate = @ExpenseDate, ExpenseCategoryId = @ExpenseCategoryId, ExpenseSubcategoryId = @ExpenseSubcategoryId,
                Amount = @Amount, Remarks = @Remarks, BusinessUnitId = @BusinessUnitId
            WHERE ExpenseId = @ExpenseId";
        var rows = await connection.ExecuteAsync(sql, expense);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int expenseId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM dbo.Expenses WHERE ExpenseId = @expenseId", new { expenseId });
        return rows > 0;
    }

    public async Task<decimal> GetTotalAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0, int? businessUnitId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT ISNULL(SUM(Amount), 0) FROM dbo.Expenses
            WHERE ExpenseDate >= @fromDate AND ExpenseDate < @toDateExclusive
              AND (@companyId = 0 OR CompanyId = @companyId)
              AND (@businessUnitId IS NULL OR BusinessUnitId = @businessUnitId)";
        return await connection.ExecuteScalarAsync<decimal>(sql, new { fromDate, toDateExclusive, companyId, businessUnitId });
    }

    public async Task<IEnumerable<ExpenseCategoryTotal>> GetSummaryByCategoryAsync(DateTime fromDate, DateTime toDateExclusive, int companyId = 0)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT ec.ExpenseCategoryName AS Category, SUM(e.Amount) AS Total
            FROM dbo.Expenses e
            INNER JOIN dbo.ExpenseCategories ec ON ec.ExpenseCategoryId = e.ExpenseCategoryId
            WHERE e.ExpenseDate >= @fromDate AND e.ExpenseDate < @toDateExclusive
              AND (@companyId = 0 OR e.CompanyId = @companyId)
            GROUP BY ec.ExpenseCategoryName
            ORDER BY SUM(e.Amount) DESC";
        return await connection.QueryAsync<ExpenseCategoryTotal>(sql, new { fromDate, toDateExclusive, companyId });
    }
}
