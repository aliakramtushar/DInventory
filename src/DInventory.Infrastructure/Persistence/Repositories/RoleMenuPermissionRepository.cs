using System.Data;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;
using Dapper;

namespace DInventory.Infrastructure.Persistence.Repositories;

public class RoleMenuPermissionRepository : IRoleMenuPermissionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RoleMenuPermissionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<MenuPermissionDto>> GetPermissionsForRoleAsync(int roleId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT m.MenuId, m.MenuKey, m.MenuName, m.Icon, m.Url, m.ParentId, m.DisplayOrder,
                   ISNULL(p.CanView, 0) AS CanView, ISNULL(p.CanCreate, 0) AS CanCreate,
                   ISNULL(p.CanEdit, 0) AS CanEdit, ISNULL(p.CanDelete, 0) AS CanDelete
            FROM dbo.Menus m
            LEFT JOIN dbo.RoleMenuPermissions p ON p.MenuId = m.MenuId AND p.RoleId = @roleId
            WHERE m.IsActive = 1
            ORDER BY m.DisplayOrder, m.MenuName";
        return await connection.QueryAsync<MenuPermissionDto>(sql, new { roleId });
    }

    public async Task<RoleMenuPermission?> GetAsync(int roleId, int menuId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM dbo.RoleMenuPermissions WHERE RoleId = @roleId AND MenuId = @menuId";
        return await connection.QuerySingleOrDefaultAsync<RoleMenuPermission>(sql, new { roleId, menuId });
    }

    public async Task SaveRolePermissionsAsync(int roleId, IEnumerable<RolePermissionUpdateItem> items)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection is not System.Data.Common.DbConnection dbConnection)
        {
            await SaveWithoutTransactionAsync(connection, roleId, items);
            return;
        }

        using var transaction = await dbConnection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync("DELETE FROM dbo.RoleMenuPermissions WHERE RoleId = @roleId", new { roleId }, transaction);

            const string insertSql = @"
                INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanCreate, CanEdit, CanDelete)
                VALUES (@RoleId, @MenuId, @CanView, @CanCreate, @CanEdit, @CanDelete)";

            foreach (var item in items.Where(i => i.CanView || i.CanCreate || i.CanEdit || i.CanDelete))
            {
                await connection.ExecuteAsync(insertSql, new
                {
                    RoleId = roleId,
                    item.MenuId,
                    item.CanView,
                    item.CanCreate,
                    item.CanEdit,
                    item.CanDelete
                }, transaction);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task SaveWithoutTransactionAsync(IDbConnection connection, int roleId, IEnumerable<RolePermissionUpdateItem> items)
    {
        await connection.ExecuteAsync("DELETE FROM dbo.RoleMenuPermissions WHERE RoleId = @roleId", new { roleId });

        const string insertSql = @"
            INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanCreate, CanEdit, CanDelete)
            VALUES (@RoleId, @MenuId, @CanView, @CanCreate, @CanEdit, @CanDelete)";

        foreach (var item in items.Where(i => i.CanView || i.CanCreate || i.CanEdit || i.CanDelete))
        {
            await connection.ExecuteAsync(insertSql, new
            {
                RoleId = roleId,
                item.MenuId,
                item.CanView,
                item.CanCreate,
                item.CanEdit,
                item.CanDelete
            });
        }
    }
}
