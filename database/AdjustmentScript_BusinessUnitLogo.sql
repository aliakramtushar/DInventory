/* =====================================================================
   DInventory - Adjustment Script: BusinessUnits.Logo / LogoContentType
   Run this ONCE against your EXISTING DInventoryDB (the one with real data).

   This does NOT touch Database.sql and does NOT drop or recreate anything.
   It only adds two nullable BusinessUnits columns:
     - Logo             VARBINARY(MAX) - the raw uploaded image bytes
     - LogoContentType  NVARCHAR(50)   - its MIME type ("image/jpeg" or
                                         "image/png"), needed to serve Logo
                                         back correctly as an image response
   Both stay NULL until a SuperAdmin/Admin uploads a logo for that business
   unit (JPG/JPEG/PNG only, 50KB max - enforced in application code, not
   here).

   Guarded (COL_LENGTH(...) IS NULL), so this script is idempotent - safe to
   run more than once, and safe to run even if it already applied.

   New installs done via the full Database.sql already include these
   columns (part of the BusinessUnits table definition) - you only need
   this file for your existing database with live data.
   ===================================================================== */

USE DInventoryDB;
GO

IF COL_LENGTH('dbo.BusinessUnits', 'Logo') IS NULL
BEGIN
    ALTER TABLE dbo.BusinessUnits ADD Logo VARBINARY(MAX) NULL, LogoContentType NVARCHAR(50) NULL;
END
GO
