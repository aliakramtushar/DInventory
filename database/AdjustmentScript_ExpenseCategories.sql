/* =====================================================================
   DInventory - Adjustment Script: Expense Categories / Expense Subcategories
   Run this ONCE against your EXISTING DInventoryDB (the one with real data).

   Replaces the old fixed 8-value Expenses.Category string list with a proper,
   company-manageable parent/child expense chart of accounts (ExpenseCategories /
   ExpenseSubcategories) - the same shape as Categories/Subcategories for Products.

   What this does, in order:
     1. Creates dbo.ExpenseCategories / dbo.ExpenseSubcategories if they don't exist.
     2. Adds Expenses.CompanyId / Expenses.BusinessUnitId if missing (these were
        missing from the original Expenses table definition entirely - a pre-existing
        gap fixed here alongside the category rework).
     3. Adds Expenses.ExpenseCategoryId / Expenses.ExpenseSubcategoryId (nullable at
        first, so existing rows aren't rejected).
     4. Backfills one ExpenseCategories row per distinct (CompanyId, Category string)
        pair actually used in dbo.Expenses, then points every existing Expense row at
        the matching new ExpenseCategoryId.
     5. Once every row is backfilled, makes ExpenseCategoryId NOT NULL and adds the
        foreign keys.
     6. Drops the old CK_Expenses_Category CHECK constraint (it would reject any new
        category string once companies start managing their own list).
     7. Adds the EXPENSECATEGORIES / EXPENSESUBCATEGORIES menu entries + role
        permissions (same access level as EXPENSES) so the new admin screens show up
        in the sidebar without needing to re-run the whole Database.sql seed.

   Does NOT drop the old Expenses.Category column or its historical data - only the
   CHECK constraint is dropped; the column itself is left in place, unused, for
   audit/reference.

   Guarded throughout (COL_LENGTH / OBJECT_ID / IF NOT EXISTS checks), so this script
   is idempotent - safe to run more than once.

   New installs done via the full Database.sql already include all of this as part of
   the table definitions - you only need this file for your existing database with
   live data.
   ===================================================================== */

USE DInventoryDB;
GO

-- 1. New tables ---------------------------------------------------------------
IF OBJECT_ID('dbo.ExpenseCategories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpenseCategories
    (
        ExpenseCategoryId    INT IDENTITY(1,1) PRIMARY KEY,
        ExpenseCategoryName  NVARCHAR(100) NOT NULL,
        Description          NVARCHAR(255) NULL,
        CompanyId            INT NOT NULL DEFAULT (0),
        BusinessUnitId       INT NULL,
        IsActive             BIT NOT NULL DEFAULT (1),
        CreatedAt            DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt            DATETIME2 NULL,
        CreatedBy            INT NULL,
        UpdatedBy            INT NULL
    );
END
GO

IF OBJECT_ID('dbo.ExpenseSubcategories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpenseSubcategories
    (
        ExpenseSubcategoryId    INT IDENTITY(1,1) PRIMARY KEY,
        ExpenseCategoryId       INT NOT NULL,
        ExpenseSubcategoryName  NVARCHAR(100) NOT NULL,
        Description             NVARCHAR(255) NULL,
        CompanyId               INT NOT NULL DEFAULT (0),
        BusinessUnitId          INT NULL,
        IsActive                BIT NOT NULL DEFAULT (1),
        CreatedAt               DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt               DATETIME2 NULL,
        CreatedBy               INT NULL,
        UpdatedBy               INT NULL,
        CONSTRAINT FK_ExpenseSubcategories_ExpenseCategories FOREIGN KEY (ExpenseCategoryId) REFERENCES dbo.ExpenseCategories(ExpenseCategoryId)
    );
END
GO

-- 2. Columns Expenses was always missing ---------------------------------------
IF COL_LENGTH('dbo.Expenses', 'CompanyId') IS NULL
BEGIN
    ALTER TABLE dbo.Expenses ADD CompanyId INT NOT NULL DEFAULT (0);
END
GO

IF COL_LENGTH('dbo.Expenses', 'BusinessUnitId') IS NULL
BEGIN
    ALTER TABLE dbo.Expenses ADD BusinessUnitId INT NULL;
END
GO

-- 3. New category columns (nullable for now - backfilled below) ----------------
IF COL_LENGTH('dbo.Expenses', 'ExpenseCategoryId') IS NULL
BEGIN
    ALTER TABLE dbo.Expenses ADD ExpenseCategoryId INT NULL;
END
GO

IF COL_LENGTH('dbo.Expenses', 'ExpenseSubcategoryId') IS NULL
BEGIN
    ALTER TABLE dbo.Expenses ADD ExpenseSubcategoryId INT NULL;
END
GO

-- 4. Backfill: one ExpenseCategories row per distinct (CompanyId, Category) pair
-- actually used, then point every Expenses row at its matching new category.
IF COL_LENGTH('dbo.Expenses', 'Category') IS NOT NULL AND COL_LENGTH('dbo.Expenses', 'ExpenseCategoryId') IS NOT NULL
BEGIN
    INSERT INTO dbo.ExpenseCategories (ExpenseCategoryName, CompanyId, IsActive, CreatedAt)
    SELECT DISTINCT e.Category, e.CompanyId, 1, SYSUTCDATETIME()
    FROM dbo.Expenses e
    WHERE e.ExpenseCategoryId IS NULL
      AND NOT EXISTS (
          SELECT 1 FROM dbo.ExpenseCategories ec
          WHERE ec.CompanyId = e.CompanyId AND ec.ExpenseCategoryName = e.Category
      );

    UPDATE e
    SET e.ExpenseCategoryId = ec.ExpenseCategoryId
    FROM dbo.Expenses e
    INNER JOIN dbo.ExpenseCategories ec ON ec.CompanyId = e.CompanyId AND ec.ExpenseCategoryName = e.Category
    WHERE e.ExpenseCategoryId IS NULL;
END
GO

-- 5. Once every row has a category, lock the column down and add the FKs.
IF NOT EXISTS (SELECT 1 FROM dbo.Expenses WHERE ExpenseCategoryId IS NULL)
BEGIN
    ALTER TABLE dbo.Expenses ALTER COLUMN ExpenseCategoryId INT NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Expenses_ExpenseCategories')
   AND NOT EXISTS (SELECT 1 FROM dbo.Expenses WHERE ExpenseCategoryId IS NULL)
BEGIN
    ALTER TABLE dbo.Expenses ADD CONSTRAINT FK_Expenses_ExpenseCategories FOREIGN KEY (ExpenseCategoryId) REFERENCES dbo.ExpenseCategories(ExpenseCategoryId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Expenses_ExpenseSubcategories')
BEGIN
    ALTER TABLE dbo.Expenses ADD CONSTRAINT FK_Expenses_ExpenseSubcategories FOREIGN KEY (ExpenseSubcategoryId) REFERENCES dbo.ExpenseSubcategories(ExpenseSubcategoryId);
END
GO

-- 6. Drop the old CHECK constraint - the Category column and its historical
-- values are left in place, unused, for audit/reference.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Expenses_Category')
BEGIN
    ALTER TABLE dbo.Expenses DROP CONSTRAINT CK_Expenses_Category;
END
GO

-- 7. Menu entries + permissions, same access level as EXPENSES itself.
IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'EXPENSECATEGORIES')
BEGIN
    DECLARE @ExpensesParentId INT = (SELECT ParentId FROM dbo.Menus WHERE MenuKey = N'EXPENSES');

    INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
    VALUES (N'EXPENSECATEGORIES', N'Expense Categories', N'bi-tags', N'/ExpenseCategories', @ExpensesParentId, 21, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'EXPENSESUBCATEGORIES')
        INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
        VALUES (N'EXPENSESUBCATEGORIES', N'Expense Subcategories', N'bi-tags', N'/ExpenseSubcategories', @ExpensesParentId, 22, 1);

    INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanCreate, CanEdit, CanDelete)
    SELECT r.RoleId, m.MenuId,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin', N'Manager') THEN 1 ELSE 0 END,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin', N'Manager') THEN 1 ELSE 0 END,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin', N'Manager') THEN 1 ELSE 0 END,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin') THEN 1 ELSE 0 END
    FROM dbo.Roles r
    CROSS JOIN dbo.Menus m
    WHERE m.MenuKey IN (N'EXPENSECATEGORIES', N'EXPENSESUBCATEGORIES')
      AND NOT EXISTS (SELECT 1 FROM dbo.RoleMenuPermissions p WHERE p.RoleId = r.RoleId AND p.MenuId = m.MenuId);
END
GO
