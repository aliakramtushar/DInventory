/* =====================================================================
   DInventory - Adjustment Script: Barcode Generator cleanup
   Run this ONCE against your EXISTING DInventoryDB (the one with real data).

   This does NOT touch Database.sql. It DOES drop columns (see below) - it
   does not drop or recreate any table, and every step is guarded so it's
   safe to run more than once.

   Barcode Generator simplification:
     1. Printed labels no longer have a configurable bar width/height - every
        barcode is drawn at a fixed size. Drops
        GeneratedBarcodeLabels.BarcodeWidth and .BarcodeHeight (and their
        default constraints, which must be dropped first).
     2. Printed labels no longer show an optional company name. Drops
        GeneratedBarcodeLabels.CompanyName.
     3. The legacy "was a company code included in the barcode text" flag is
        no longer meaningful (the feature it tracked is gone too). Drops
        GeneratedBarcodeLabels.IncludeCompanyCode (and its default
        constraint).
     4. Price code is now purely a display tag - shown as
        "ProductName (PriceCode)" wherever the label is printed/listed -
        rather than being spliced into the barcode's own digits. No column
        change needed for this: PriceCode already existed as a plain
        NVARCHAR column and keeps being used the same way, just formatted
        differently at display time.
     5. Every barcode must be unique: adds a UNIQUE constraint on
        GeneratedBarcodeLabels.Barcode and ProductVariants.Barcode if either
        table doesn't already have one (both already declare
        NOT NULL UNIQUE for brand-new installs - this covers a table
        created by an older version of Database.sql that didn't).

   IMPORTANT: step 5 will fail (with a message printed, not a silent skip)
   if either table already has duplicate barcode values - resolve those
   duplicates by hand first, then re-run this script.
   ===================================================================== */

USE DInventoryDB;
GO

-- Step 1: drop the configurable bar width/height columns (and their default constraints).
IF OBJECT_ID('dbo.DF_GBL_BarcodeWidth', 'D') IS NOT NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels DROP CONSTRAINT DF_GBL_BarcodeWidth;
END
GO

IF OBJECT_ID('dbo.DF_GBL_BarcodeHeight', 'D') IS NOT NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels DROP CONSTRAINT DF_GBL_BarcodeHeight;
END
GO

IF COL_LENGTH('dbo.GeneratedBarcodeLabels', 'BarcodeWidth') IS NOT NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels DROP COLUMN BarcodeWidth;
END
GO

IF COL_LENGTH('dbo.GeneratedBarcodeLabels', 'BarcodeHeight') IS NOT NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels DROP COLUMN BarcodeHeight;
END
GO

-- Step 2: drop the optional company name column.
IF COL_LENGTH('dbo.GeneratedBarcodeLabels', 'CompanyName') IS NOT NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels DROP COLUMN CompanyName;
END
GO

-- Step 3: drop the legacy "included company code" flag (and its default constraint).
IF OBJECT_ID('dbo.DF_GBL_IncludeCompanyCode', 'D') IS NOT NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels DROP CONSTRAINT DF_GBL_IncludeCompanyCode;
END
GO

IF COL_LENGTH('dbo.GeneratedBarcodeLabels', 'IncludeCompanyCode') IS NOT NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels DROP COLUMN IncludeCompanyCode;
END
GO

-- Step 5: every barcode must be unique.
IF NOT EXISTS (
        SELECT 1 FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID('dbo.GeneratedBarcodeLabels') AND i.is_unique = 1 AND c.name = 'Barcode'
   )
BEGIN
    BEGIN TRY
        ALTER TABLE dbo.GeneratedBarcodeLabels ADD CONSTRAINT UQ_GBL_Barcode UNIQUE (Barcode);
    END TRY
    BEGIN CATCH
        PRINT 'WARNING: Could not add a UNIQUE constraint on GeneratedBarcodeLabels.Barcode (likely duplicate barcodes already exist). Resolve duplicates manually, then re-run this script. Error: ' + ERROR_MESSAGE();
    END CATCH
END
GO

IF NOT EXISTS (
        SELECT 1 FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID('dbo.ProductVariants') AND i.is_unique = 1 AND c.name = 'Barcode'
   )
BEGIN
    BEGIN TRY
        ALTER TABLE dbo.ProductVariants ADD CONSTRAINT UQ_PV_Barcode UNIQUE (Barcode);
    END TRY
    BEGIN CATCH
        PRINT 'WARNING: Could not add a UNIQUE constraint on ProductVariants.Barcode (likely duplicate barcodes already exist). Resolve duplicates manually, then re-run this script. Error: ' + ERROR_MESSAGE();
    END CATCH
END
GO
