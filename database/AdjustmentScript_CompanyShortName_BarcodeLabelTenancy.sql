/* =====================================================================
   DInventory - Adjustment Script: Company Short Name + Barcode Label Tenancy
   Run this ONCE against your EXISTING DInventoryDB (the one with real data).

   This does NOT touch Database.sql and does NOT drop or recreate anything.
   It only:
     1. Adds a mandatory, unique Companies.ShortName column (backfilled to
        "COM<CompanyId>" for any company that doesn't have one yet - go into
        Companies > Edit afterwards and set a real short name, e.g. "DINV").
        This short name is now used as the barcode prefix for every barcode
        generated under that company (e.g. DINV000000123).
     2. Adds CompanyId (mandatory, backfilled to 1) + BusinessUnitId (optional)
        to dbo.GeneratedBarcodeLabels, the same way the original multi-tenancy
        migration did for every other company-owned table.

   Every step is guarded (COL_LENGTH(...) IS NULL / IF NOT EXISTS(...)), so
   this script is idempotent - safe to run more than once, and safe to run
   even if part of it already applied.

   New installs done via the full Database.sql already include both of these
   changes (ShortName is part of the Companies table definition, and
   GeneratedBarcodeLabels is in the CompanyId/BusinessUnitId table list) - you
   only need this file for your existing database with live data.
   ===================================================================== */

USE DInventoryDB;
GO

/* =====================================================================
   1. Companies.ShortName - mandatory, unique, used as the barcode prefix.
   ===================================================================== */
IF COL_LENGTH('dbo.Companies', 'ShortName') IS NULL
BEGIN
    ALTER TABLE dbo.Companies ADD ShortName NVARCHAR(15) NULL;
END
GO

-- Backfill: guarantees uniqueness (CompanyId is a PK) so the NOT NULL + UNIQUE
-- constraint below can never fail on existing data. Replace these with real,
-- meaningful short names afterwards via Companies > Edit whenever convenient.
UPDATE dbo.Companies
SET ShortName = N'COM' + CAST(CompanyId AS NVARCHAR(10))
WHERE ShortName IS NULL OR LEN(LTRIM(RTRIM(ShortName))) < 3;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Companies') AND name = 'ShortName' AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.Companies ALTER COLUMN ShortName NVARCHAR(15) NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Companies_ShortName')
BEGIN
    ALTER TABLE dbo.Companies ADD CONSTRAINT UQ_Companies_ShortName UNIQUE (ShortName);
END
GO

/* =====================================================================
   2. GeneratedBarcodeLabels - give it the same mandatory CompanyId (backfilled
      to CompanyId = 1) + optional BusinessUnitId every other company-owned
      table already has.
   ===================================================================== */
IF OBJECT_ID('dbo.GeneratedBarcodeLabels', 'U') IS NOT NULL AND COL_LENGTH('dbo.GeneratedBarcodeLabels', 'CompanyId') IS NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels ADD CompanyId INT NULL, BusinessUnitId INT NULL;

    UPDATE dbo.GeneratedBarcodeLabels SET CompanyId = 1 WHERE CompanyId IS NULL;

    ALTER TABLE dbo.GeneratedBarcodeLabels ALTER COLUMN CompanyId INT NOT NULL;

    ALTER TABLE dbo.GeneratedBarcodeLabels ADD CONSTRAINT FK_GeneratedBarcodeLabels_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId);
    ALTER TABLE dbo.GeneratedBarcodeLabels ADD CONSTRAINT FK_GeneratedBarcodeLabels_BusinessUnits FOREIGN KEY (BusinessUnitId) REFERENCES dbo.BusinessUnits(BusinessUnitId);
END
GO

PRINT 'Company ShortName + GeneratedBarcodeLabels tenancy adjustment complete.';
GO
