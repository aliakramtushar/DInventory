/* =====================================================================
   DInventory - Adjustment Script: Companies.Language
   Run this ONCE against your EXISTING DInventoryDB (the one with real data).

   This does NOT touch Database.sql and does NOT drop or recreate anything.
   It only adds a Companies.Language TINYINT column (defaulted to 0,
   backfilled to 0 for every existing company) - a fixed two-value language
   tag for the company: 0 = English, 1 = Bangla (see the CompanyLanguage
   enum in DInventory.Domain.Enums). It is just a label - no
   translation/localization behavior is driven by this value anywhere else
   in the app.

   Guarded (COL_LENGTH(...) IS NULL), so this script is idempotent - safe to
   run more than once, and safe to run even if it already applied.

   New installs done via the full Database.sql already include this column
   (part of the Companies table definition) - you only need this file for
   your existing database with live data.
   ===================================================================== */

USE DInventoryDB;
GO

IF COL_LENGTH('dbo.Companies', 'Language') IS NULL
BEGIN
    ALTER TABLE dbo.Companies ADD Language TINYINT NOT NULL CONSTRAINT DF_Companies_Language DEFAULT (0);
END
GO
