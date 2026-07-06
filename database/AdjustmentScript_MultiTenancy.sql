/* =====================================================================
   DInventory - Multi-Tenancy Adjustment Script (Companies / Business Units)
   Run this ONCE against your EXISTING DInventoryDB (the one with real data).

   This does NOT touch Database.sql and does NOT drop or recreate anything.
   It only:
     1. Creates dbo.Companies and dbo.BusinessUnits (if they don't already exist).
     2. Seeds CompanyId 0 ("Super Admin / All Companies") and CompanyId 1
        ("DInventory") - everything you already have gets attached to
        CompanyId 1 so nothing is orphaned.
     3. Adds a mandatory CompanyId + optional BusinessUnitId column to every
        company-owned table, backfilling existing rows to CompanyId 1 before
        making the column NOT NULL (safe two-step: nullable -> backfill ->
        NOT NULL), then adds the FK constraints.
     4. Promotes your existing "superadmin" user (by role, not username) to
        CompanyId 0 so it keeps seeing every company after this runs.
     5. Adds "Companies" and "Business Units" sidebar menu entries/permissions.

   Every step is guarded (IF OBJECT_ID(...) IS NULL / COL_LENGTH(...) IS NULL /
   IF NOT EXISTS(...)), so this script is idempotent - safe to run more than
   once, and safe to run even if part of it already applied. Nothing here can
   run twice or double-apply.

   New installs done via the full Database.sql already include this section
   (it was appended there too) - you only need this file for your existing
   database with live data, so you don't have to re-run the whole creation
   script.
   ===================================================================== */

USE DInventoryDB;
GO

/* =====================================================================
   1. COMPANIES / BUSINESS UNITS TABLES
   ===================================================================== */
IF OBJECT_ID('dbo.Companies', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Companies
    (
        CompanyId     INT IDENTITY(0,1) PRIMARY KEY,
        CompanyName   NVARCHAR(150) NOT NULL,
        Phone         NVARCHAR(30) NULL,
        Email         NVARCHAR(150) NULL,
        Address       NVARCHAR(255) NULL,
        IsActive      BIT NOT NULL DEFAULT (1),
        CreatedAt     DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt     DATETIME2 NULL,
        CreatedBy     INT NULL,
        UpdatedBy     INT NULL
    );
END
GO

IF OBJECT_ID('dbo.BusinessUnits', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BusinessUnits
    (
        BusinessUnitId    INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId         INT NOT NULL,
        BusinessUnitName  NVARCHAR(150) NOT NULL,
        IsActive          BIT NOT NULL DEFAULT (1),
        CreatedAt         DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt         DATETIME2 NULL,
        CreatedBy         INT NULL,
        UpdatedBy         INT NULL,
        CONSTRAINT FK_BusinessUnits_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT UQ_BusinessUnits_CompanyName UNIQUE (CompanyId, BusinessUnitName)
    );
END
GO

/* =====================================================================
   2. SEED: row 1 becomes CompanyId 0 (superuser/system, sees every company),
      row 2 becomes CompanyId 1 (the real default tenant - everything you
      already have belongs here after step 3 below).
   ===================================================================== */
IF NOT EXISTS (SELECT 1 FROM dbo.Companies)
BEGIN
    INSERT INTO dbo.Companies (CompanyName, IsActive) VALUES (N'Super Admin / All Companies', 1);
    INSERT INTO dbo.Companies (CompanyName, IsActive) VALUES (N'DInventory', 1);
END
GO

/* =====================================================================
   3. Give every existing company-owned table a mandatory CompanyId
      (backfilled to CompanyId = 1) + an optional BusinessUnitId.
   ===================================================================== */
DECLARE @t TABLE (TableName SYSNAME);
INSERT INTO @t (TableName) VALUES
    (N'Users'), (N'Categories'), (N'Subcategories'), (N'Brands'), (N'Sizes'), (N'Colors'),
    (N'Products'), (N'Customers'), (N'Suppliers'), (N'SalesOrders'), (N'Purchases'),
    (N'SalesReturns'), (N'PurchaseReturns'), (N'Expenses'), (N'ContentPages'), (N'LoyaltySettings');

DECLARE @tbl SYSNAME, @sql NVARCHAR(MAX);
DECLARE tbl_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT TableName FROM @t;
OPEN tbl_cursor;
FETCH NEXT FROM tbl_cursor INTO @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID('dbo.' + @tbl, 'U') IS NOT NULL AND COL_LENGTH('dbo.' + @tbl, 'CompanyId') IS NULL
    BEGIN
        SET @sql = N'ALTER TABLE dbo.' + @tbl + N' ADD CompanyId INT NULL, BusinessUnitId INT NULL;';
        EXEC sp_executesql @sql;

        SET @sql = N'UPDATE dbo.' + @tbl + N' SET CompanyId = 1 WHERE CompanyId IS NULL;';
        EXEC sp_executesql @sql;

        SET @sql = N'ALTER TABLE dbo.' + @tbl + N' ALTER COLUMN CompanyId INT NOT NULL;';
        EXEC sp_executesql @sql;

        SET @sql = N'ALTER TABLE dbo.' + @tbl + N' ADD CONSTRAINT FK_' + @tbl + N'_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId);';
        EXEC sp_executesql @sql;

        SET @sql = N'ALTER TABLE dbo.' + @tbl + N' ADD CONSTRAINT FK_' + @tbl + N'_BusinessUnits FOREIGN KEY (BusinessUnitId) REFERENCES dbo.BusinessUnits(BusinessUnitId);';
        EXEC sp_executesql @sql;
    END
    FETCH NEXT FROM tbl_cursor INTO @tbl;
END
CLOSE tbl_cursor;
DEALLOCATE tbl_cursor;
GO

/* =====================================================================
   4. Promote the existing SuperAdmin-role user(s) to CompanyId 0 so they
      keep seeing every company after this migration. Everyone else stays
      on CompanyId 1 (set in step 3 above).
   ===================================================================== */
IF COL_LENGTH('dbo.Users', 'CompanyId') IS NOT NULL
BEGIN
    UPDATE u SET u.CompanyId = 0
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
    WHERE r.RoleName = N'SuperAdmin';
END
GO

PRINT 'Company / BusinessUnit multi-tenancy migration complete.';
GO

/* =====================================================================
   5. Sidebar menu entries: Companies (SuperAdmin only) + Business Units
      (SuperAdmin/Admin) under the existing Admin group.
   ===================================================================== */
IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'COMPANIES')
BEGIN
    DECLARE @GAdmin5 INT = (SELECT MenuId FROM dbo.Menus WHERE MenuKey = N'GROUP_ADMIN');
    IF @GAdmin5 IS NOT NULL
    BEGIN
        INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
        VALUES (N'COMPANIES', N'Companies', N'bi-building', N'/Companies', @GAdmin5, 1, 1);

        INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
        VALUES (N'BUSINESSUNITS', N'Business Units', N'bi-diagram-3', N'/BusinessUnits', @GAdmin5, 2, 1);
    END

    INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanCreate, CanEdit, CanDelete)
    SELECT r.RoleId, m.MenuId,
        CASE WHEN m.MenuKey = N'COMPANIES' AND r.RoleName = N'SuperAdmin' THEN 1
             WHEN m.MenuKey = N'BUSINESSUNITS' AND r.RoleName IN (N'SuperAdmin', N'Admin') THEN 1
             ELSE 0 END,
        CASE WHEN m.MenuKey = N'COMPANIES' AND r.RoleName = N'SuperAdmin' THEN 1
             WHEN m.MenuKey = N'BUSINESSUNITS' AND r.RoleName IN (N'SuperAdmin', N'Admin') THEN 1
             ELSE 0 END,
        CASE WHEN m.MenuKey = N'COMPANIES' AND r.RoleName = N'SuperAdmin' THEN 1
             WHEN m.MenuKey = N'BUSINESSUNITS' AND r.RoleName IN (N'SuperAdmin', N'Admin') THEN 1
             ELSE 0 END,
        CASE WHEN m.MenuKey = N'COMPANIES' AND r.RoleName = N'SuperAdmin' THEN 1
             WHEN m.MenuKey = N'BUSINESSUNITS' AND r.RoleName = N'SuperAdmin' THEN 1
             ELSE 0 END
    FROM dbo.Roles r
    CROSS JOIN dbo.Menus m
    WHERE m.MenuKey IN (N'COMPANIES', N'BUSINESSUNITS')
      AND NOT EXISTS (SELECT 1 FROM dbo.RoleMenuPermissions p WHERE p.RoleId = r.RoleId AND p.MenuId = m.MenuId);

    UPDATE g
    SET CanView = 1
    FROM dbo.RoleMenuPermissions g
    WHERE g.MenuId IN (SELECT MenuId FROM dbo.Menus WHERE ParentId IS NULL AND MenuKey LIKE N'GROUP_%')
      AND EXISTS (
          SELECT 1 FROM dbo.RoleMenuPermissions child
          INNER JOIN dbo.Menus cm ON cm.MenuId = child.MenuId
          WHERE cm.ParentId = g.MenuId AND child.RoleId = g.RoleId AND child.CanView = 1
      );
END
GO

PRINT 'Companies / Business Units menu entries installed.';
GO
