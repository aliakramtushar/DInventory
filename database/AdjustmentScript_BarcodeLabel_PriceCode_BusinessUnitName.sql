/* =====================================================================
   DInventory - Adjustment Script: Barcode Label Price Code + Business Unit Name
   Run this ONCE against your EXISTING DInventoryDB (the one with real data).

   This does NOT touch Database.sql and does NOT drop or recreate anything.
   It only adds three optional/defaulted columns to dbo.GeneratedBarcodeLabels
   to support the Barcode Generator page's new options:

     1. BusinessUnitName NVARCHAR(150) NULL
        - Snapshot of the business unit name at generation time, purely for
          reference/display (e.g. on the History page). Optional - a label
          can be generated without picking one. Never appears inside the
          barcode text itself.

     2. PriceCode NVARCHAR(20) NOT NULL, default '000'
        - The middle segment of the barcode: CompanyCode-PriceCode-GeneratedCode.
          Optional for the user to type a custom value; defaults to "000" when
          left blank. Existing rows are backfilled to '000'.

     3. IncludeCompanyCode BIT NOT NULL, default 1
        - Whether the company's short code (Companies.ShortName) was included
          as the first segment of the barcode when it was generated. Existing
          rows are backfilled to 1 (true), since every barcode generated
          before this change always included the company code.

   Every step is guarded (COL_LENGTH(...) IS NULL), so this script is
   idempotent - safe to run more than once, and safe to run even if part of
   it already applied.

   New installs done via the full Database.sql already include all three of
   these columns - you only need this file for your existing database with
   live data.
   ===================================================================== */

USE DInventoryDB;
GO

IF OBJECT_ID('dbo.GeneratedBarcodeLabels', 'U') IS NOT NULL AND COL_LENGTH('dbo.GeneratedBarcodeLabels', 'BusinessUnitName') IS NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels ADD BusinessUnitName NVARCHAR(150) NULL;
END
GO

IF OBJECT_ID('dbo.GeneratedBarcodeLabels', 'U') IS NOT NULL AND COL_LENGTH('dbo.GeneratedBarcodeLabels', 'PriceCode') IS NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels ADD PriceCode NVARCHAR(20) NOT NULL CONSTRAINT DF_GBL_PriceCode DEFAULT ('000');
END
GO

IF OBJECT_ID('dbo.GeneratedBarcodeLabels', 'U') IS NOT NULL AND COL_LENGTH('dbo.GeneratedBarcodeLabels', 'IncludeCompanyCode') IS NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels ADD IncludeCompanyCode BIT NOT NULL CONSTRAINT DF_GBL_IncludeCompanyCode DEFAULT (1);
END
GO

PRINT 'GeneratedBarcodeLabels PriceCode + BusinessUnitName + IncludeCompanyCode adjustment complete.';
GO
