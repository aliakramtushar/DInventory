/* =====================================================================
   DInventory - Adjustment Script: Supplier Remarks + Purchase Discount + Business Unit Address
   Run this ONCE against your EXISTING DInventoryDB (the one with real data).

   This does NOT touch Database.sql and does NOT drop or recreate anything.
   It only adds three optional/defaulted columns:

     1. dbo.Suppliers.Remarks NVARCHAR(1000) NULL
        - Free-text notes field on the supplier record. Optional.

     2. dbo.Purchases.Discount DECIMAL(18,2) NOT NULL, default 0
        - A header-level discount applied to a purchase invoice. Reduces the
          amount actually owed to the supplier (Due = TotalAmount - Discount -
          PaidAmount - ReturnedAmount). Existing rows are backfilled to 0, so
          past purchases are unaffected.

     3. dbo.BusinessUnits.Address NVARCHAR(255) NULL
        - Optional postal address for a business unit/branch.

   Every step is guarded (COL_LENGTH(...) IS NULL), so this script is
   idempotent - safe to run more than once, and safe to run even if part of
   it already applied.

   New installs done via the full Database.sql already include all three of
   these columns - you only need this file for your existing database with
   live data.
   ===================================================================== */

USE DInventoryDB;
GO

IF OBJECT_ID('dbo.Suppliers', 'U') IS NOT NULL AND COL_LENGTH('dbo.Suppliers', 'Remarks') IS NULL
BEGIN
    ALTER TABLE dbo.Suppliers ADD Remarks NVARCHAR(1000) NULL;
END
GO

IF OBJECT_ID('dbo.Purchases', 'U') IS NOT NULL AND COL_LENGTH('dbo.Purchases', 'Discount') IS NULL
BEGIN
    ALTER TABLE dbo.Purchases ADD Discount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Purchases_Discount DEFAULT (0);
END
GO

IF OBJECT_ID('dbo.BusinessUnits', 'U') IS NOT NULL AND COL_LENGTH('dbo.BusinessUnits', 'Address') IS NULL
BEGIN
    ALTER TABLE dbo.BusinessUnits ADD Address NVARCHAR(255) NULL;
END
GO

PRINT 'Suppliers.Remarks + Purchases.Discount + BusinessUnits.Address adjustment complete.';
GO
