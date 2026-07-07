/* =====================================================================
   DInventory - Adjustment Script: Companies.HasECommerce
   Run this ONCE against your EXISTING DInventoryDB (the one with real data).

   This does NOT touch Database.sql and does NOT drop or recreate anything.
   It only adds a Companies.HasECommerce BIT column (defaulted to 0/false,
   backfilled to 0 for every existing company) that gates whether that
   company's products can use the "Show on public website" / "Show price on
   website" fields - SuperAdmin can always see/set both regardless of this
   flag; every other user only sees them when their company has this on.

   Guarded (COL_LENGTH(...) IS NULL), so this script is idempotent - safe to
   run more than once, and safe to run even if it already applied.

   New installs done via the full Database.sql already include this column
   (part of the Companies table definition) - you only need this file for
   your existing database with live data.
   ===================================================================== */

USE DInventoryDB;
GO

IF COL_LENGTH('dbo.Companies', 'HasECommerce') IS NULL
BEGIN
    ALTER TABLE dbo.Companies ADD HasECommerce BIT NOT NULL DEFAULT (0);
END
GO
