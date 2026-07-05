/* =====================================================================
   DInventory - Fashion House Inventory + Sales Management System
   Database creation script for SQL Server / LocalDB
   Target: (localdb)\MSSQLLocalDB  (Windows Authentication)

   *** BREAKING SCHEMA CHANGE ***
   This version adds Brands, Sizes, per-size Product Variants (with their
   own barcode) and barcode label generation. Stock, stock transactions and
   sales items now key off a product VARIANT (size), not the product
   directly. If you already have DInventoryDB from an earlier version,
   the simplest and safest path is:
       DROP DATABASE DInventoryDB;
   then just restart the app (or re-run this whole script) - everything
   below is idempotent and will recreate it cleanly on the new schema.
   ===================================================================== */

IF DB_ID('DInventoryDB') IS NULL
BEGIN
    CREATE DATABASE DInventoryDB;
END
GO

USE DInventoryDB;
GO

/* =====================================================================
   1. ROLES
   ===================================================================== */
IF OBJECT_ID('dbo.Roles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        RoleId        INT IDENTITY(1,1) PRIMARY KEY,
        RoleName      NVARCHAR(50)  NOT NULL UNIQUE,
        Description   NVARCHAR(255) NULL,
        IsSystemRole  BIT           NOT NULL DEFAULT (0),
        IsActive      BIT           NOT NULL DEFAULT (1),
        CreatedAt     DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt     DATETIME2     NULL
    );
END
GO

/* =====================================================================
   2. USERS
   ===================================================================== */
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        UserId         INT IDENTITY(1,1) PRIMARY KEY,
        Username       NVARCHAR(50)  NOT NULL UNIQUE,
        Email          NVARCHAR(150) NOT NULL UNIQUE,
        PasswordHash   NVARCHAR(255) NOT NULL,
        FullName       NVARCHAR(150) NOT NULL,
        RoleId         INT           NOT NULL,
        IsActive       BIT           NOT NULL DEFAULT (1),
        ProfileImage   NVARCHAR(255) NULL,
        LastLoginAt    DATETIME2     NULL,
        CreatedAt      DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt      DATETIME2     NULL,
        CreatedBy      INT           NULL,
        UpdatedBy      INT           NULL,
        CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
    );
END
GO

/* =====================================================================
   3. MENUS
   ===================================================================== */
IF OBJECT_ID('dbo.Menus', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Menus
    (
        MenuId        INT IDENTITY(1,1) PRIMARY KEY,
        MenuKey       NVARCHAR(50)  NOT NULL UNIQUE,
        MenuName      NVARCHAR(100) NOT NULL,
        Icon          NVARCHAR(50)  NULL,
        Url           NVARCHAR(255) NULL,
        ParentId      INT           NULL,
        DisplayOrder  INT           NOT NULL DEFAULT (0),
        IsActive      BIT           NOT NULL DEFAULT (1),
        CONSTRAINT FK_Menus_Parent FOREIGN KEY (ParentId) REFERENCES dbo.Menus(MenuId)
    );
END
GO

/* =====================================================================
   4. ROLE MENU PERMISSIONS
   ===================================================================== */
IF OBJECT_ID('dbo.RoleMenuPermissions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoleMenuPermissions
    (
        PermissionId  INT IDENTITY(1,1) PRIMARY KEY,
        RoleId        INT NOT NULL,
        MenuId        INT NOT NULL,
        CanView       BIT NOT NULL DEFAULT (0),
        CanCreate     BIT NOT NULL DEFAULT (0),
        CanEdit       BIT NOT NULL DEFAULT (0),
        CanDelete     BIT NOT NULL DEFAULT (0),
        CONSTRAINT FK_RMP_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId) ON DELETE CASCADE,
        CONSTRAINT FK_RMP_Menus FOREIGN KEY (MenuId) REFERENCES dbo.Menus(MenuId) ON DELETE CASCADE,
        CONSTRAINT UQ_RMP UNIQUE (RoleId, MenuId)
    );
END
GO

/* =====================================================================
   5. BRANDS / SIZES (new: fashion catalog attributes)
   ===================================================================== */
IF OBJECT_ID('dbo.Brands', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Brands
    (
        BrandId       INT IDENTITY(1,1) PRIMARY KEY,
        BrandName     NVARCHAR(100) NOT NULL UNIQUE,
        Description   NVARCHAR(255) NULL,
        IsActive      BIT NOT NULL DEFAULT (1),
        CreatedAt     DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt     DATETIME2 NULL,
        CreatedBy     INT NULL,
        UpdatedBy     INT NULL
    );
END
GO

IF OBJECT_ID('dbo.Sizes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sizes
    (
        SizeId        INT IDENTITY(1,1) PRIMARY KEY,
        SizeName      NVARCHAR(30) NOT NULL UNIQUE,
        DisplayOrder  INT NOT NULL DEFAULT (0),
        IsActive      BIT NOT NULL DEFAULT (1),
        CreatedAt     DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME())
    );
END
GO

/* =====================================================================
   6. CATEGORIES / SUBCATEGORIES
   ===================================================================== */
IF OBJECT_ID('dbo.Categories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories
    (
        CategoryId    INT IDENTITY(1,1) PRIMARY KEY,
        CategoryName  NVARCHAR(100) NOT NULL,
        Description   NVARCHAR(255) NULL,
        IsActive      BIT NOT NULL DEFAULT (1),
        CreatedAt     DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt     DATETIME2 NULL,
        CreatedBy     INT NULL,
        UpdatedBy     INT NULL
    );
END
GO

IF OBJECT_ID('dbo.Subcategories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Subcategories
    (
        SubcategoryId    INT IDENTITY(1,1) PRIMARY KEY,
        CategoryId       INT NOT NULL,
        SubcategoryName  NVARCHAR(100) NOT NULL,
        Description      NVARCHAR(255) NULL,
        IsActive         BIT NOT NULL DEFAULT (1),
        CreatedAt        DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt        DATETIME2 NULL,
        CreatedBy        INT NULL,
        UpdatedBy        INT NULL,
        CONSTRAINT FK_Subcategories_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId)
    );
END
GO

/* =====================================================================
   7. PRODUCTS  (style-level: name, brand, category - NOT sellable by itself)
   ===================================================================== */
IF OBJECT_ID('dbo.Products', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        ProductId          INT IDENTITY(1,1) PRIMARY KEY,
        ProductCode        NVARCHAR(50)  NOT NULL UNIQUE,
        ProductName        NVARCHAR(150) NOT NULL,
        CategoryId         INT NOT NULL,
        SubcategoryId      INT NULL,
        BrandId            INT NULL,
        Unit               NVARCHAR(30) NULL,
        Description        NVARCHAR(500) NULL,
        ImagePath          NVARCHAR(255) NULL,
        ReorderLevel       INT NOT NULL DEFAULT (5),
        IsShowOnWebsite    BIT NOT NULL DEFAULT (0),
        ShowPriceOnWebsite BIT NOT NULL DEFAULT (1),
        IsActive           BIT NOT NULL DEFAULT (1),
        CreatedAt          DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt          DATETIME2 NULL,
        CreatedBy          INT NULL,
        UpdatedBy          INT NULL,
        CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId),
        CONSTRAINT FK_Products_Subcategories FOREIGN KEY (SubcategoryId) REFERENCES dbo.Subcategories(SubcategoryId),
        CONSTRAINT FK_Products_Brands FOREIGN KEY (BrandId) REFERENCES dbo.Brands(BrandId)
    );
END
GO

-- Safe additive upgrade path for an existing Products table created before Brand/Website columns existed.
IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL AND COL_LENGTH('dbo.Products', 'BrandId') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD BrandId INT NULL;
    ALTER TABLE dbo.Products ADD CONSTRAINT FK_Products_Brands FOREIGN KEY (BrandId) REFERENCES dbo.Brands(BrandId);
END
GO

IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL AND COL_LENGTH('dbo.Products', 'IsShowOnWebsite') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD IsShowOnWebsite BIT NOT NULL DEFAULT (0);
END
GO

IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL AND COL_LENGTH('dbo.Products', 'ShowPriceOnWebsite') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD ShowPriceOnWebsite BIT NOT NULL DEFAULT (1);
END
GO

/* =====================================================================
   8. PRODUCT VARIANTS  (the sellable unit: one row per Product + Size,
      each with its own unique barcode used for scanning at entry & sale)
   ===================================================================== */
IF OBJECT_ID('dbo.ProductVariants', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductVariants
    (
        ProductVariantId  INT IDENTITY(1,1) PRIMARY KEY,
        ProductId         INT NOT NULL,
        SizeId            INT NOT NULL,
        Barcode           NVARCHAR(50) NOT NULL UNIQUE,
        SKU               NVARCHAR(50) NULL,
        ReorderLevel      INT NOT NULL DEFAULT (5),
        IsActive          BIT NOT NULL DEFAULT (1),
        CreatedAt         DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CreatedBy         INT NULL,
        CONSTRAINT FK_ProductVariants_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
        CONSTRAINT FK_ProductVariants_Sizes FOREIGN KEY (SizeId) REFERENCES dbo.Sizes(SizeId),
        CONSTRAINT UQ_ProductVariants_ProductSize UNIQUE (ProductId, SizeId)
    );
END
GO

/* =====================================================================
   9. PRICE  (kept at product/style level - same price across sizes)
   ===================================================================== */
IF OBJECT_ID('dbo.Price', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Price
    (
        PriceId        INT IDENTITY(1,1) PRIMARY KEY,
        ProductId      INT NOT NULL,
        CostPrice      DECIMAL(18,2) NOT NULL,
        SellingPrice   DECIMAL(18,2) NOT NULL,
        EffectiveFrom  DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        IsActive       BIT NOT NULL DEFAULT (1),
        CreatedAt      DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CreatedBy      INT NULL,
        CONSTRAINT FK_Price_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId)
    );
END
GO

/* =====================================================================
   10. STOCK  (per variant / per size)
   ===================================================================== */
IF OBJECT_ID('dbo.Stock', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Stock
    (
        StockId           INT IDENTITY(1,1) PRIMARY KEY,
        ProductVariantId  INT NOT NULL UNIQUE,
        QuantityOnHand    INT NOT NULL DEFAULT (0),
        UpdatedAt         DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Stock_ProductVariants FOREIGN KEY (ProductVariantId) REFERENCES dbo.ProductVariants(ProductVariantId)
    );
END
GO

/* =====================================================================
   11. STOCK TRANSACTIONS  (per variant / per size)
   ===================================================================== */
IF OBJECT_ID('dbo.StockTransactions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockTransactions
    (
        TransactionId     INT IDENTITY(1,1) PRIMARY KEY,
        ProductVariantId  INT NOT NULL,
        TransactionType   NVARCHAR(20) NOT NULL, -- IN, OUT, ADJUSTMENT
        Quantity          INT NOT NULL,
        ReferenceType     NVARCHAR(30) NULL,     -- SALE, PURCHASE, MANUAL
        ReferenceId       INT NULL,
        Remarks           NVARCHAR(255) NULL,
        CreatedAt         DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CreatedBy         INT NULL,
        CONSTRAINT FK_StockTx_ProductVariants FOREIGN KEY (ProductVariantId) REFERENCES dbo.ProductVariants(ProductVariantId)
    );
END
GO

/* =====================================================================
   12. GENERATED BARCODE LABELS  (pre-print log for the barcode generator
       page - print a tag with a fresh barcode + price BEFORE the item is
       formally entered into the system, then tag the physical garment)
   ===================================================================== */
IF OBJECT_ID('dbo.GeneratedBarcodeLabels', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GeneratedBarcodeLabels
    (
        LabelId                 INT IDENTITY(1,1) PRIMARY KEY,
        Barcode                 NVARCHAR(50) NOT NULL UNIQUE,
        ProductName             NVARCHAR(150) NOT NULL,
        BrandName               NVARCHAR(100) NULL,
        SizeName                NVARCHAR(30) NULL,
        CompanyName             NVARCHAR(150) NULL,
        Price                   DECIMAL(18,2) NULL,
        BarcodeWidth            INT NOT NULL DEFAULT (2),
        BarcodeHeight           INT NOT NULL DEFAULT (50),
        IsLinked                BIT NOT NULL DEFAULT (0),
        LinkedProductVariantId  INT NULL,
        CreatedAt               DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CreatedBy               INT NULL,
        CONSTRAINT FK_GBL_ProductVariants FOREIGN KEY (LinkedProductVariantId) REFERENCES dbo.ProductVariants(ProductVariantId)
    );
END
GO

-- Safe additive upgrade path for an existing GeneratedBarcodeLabels table created before the
-- Company Name / configurable barcode size fields existed.
IF OBJECT_ID('dbo.GeneratedBarcodeLabels', 'U') IS NOT NULL AND COL_LENGTH('dbo.GeneratedBarcodeLabels', 'CompanyName') IS NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels ADD CompanyName NVARCHAR(150) NULL;
END
GO

IF OBJECT_ID('dbo.GeneratedBarcodeLabels', 'U') IS NOT NULL AND COL_LENGTH('dbo.GeneratedBarcodeLabels', 'BarcodeWidth') IS NULL
BEGIN
    ALTER TABLE dbo.GeneratedBarcodeLabels ADD BarcodeWidth INT NOT NULL CONSTRAINT DF_GBL_BarcodeWidth DEFAULT (2);
    ALTER TABLE dbo.GeneratedBarcodeLabels ADD BarcodeHeight INT NOT NULL CONSTRAINT DF_GBL_BarcodeHeight DEFAULT (50);
END
GO

/* =====================================================================
   13. CUSTOMERS / SALES
   ===================================================================== */
IF OBJECT_ID('dbo.Customers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        CustomerId    INT IDENTITY(1,1) PRIMARY KEY,
        CustomerName  NVARCHAR(150) NOT NULL,
        Phone         NVARCHAR(30) NULL,
        Email         NVARCHAR(150) NULL,
        Address       NVARCHAR(255) NULL,
        IsActive      BIT NOT NULL DEFAULT (1),
        CreatedAt     DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID('dbo.SalesOrders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesOrders
    (
        SalesOrderId    INT IDENTITY(1,1) PRIMARY KEY,
        InvoiceNo       NVARCHAR(30) NOT NULL UNIQUE,
        CustomerId      INT NULL,
        SaleDate        DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        SubTotal        DECIMAL(18,2) NOT NULL DEFAULT (0),
        DiscountAmount  DECIMAL(18,2) NOT NULL DEFAULT (0),
        TaxAmount       DECIMAL(18,2) NOT NULL DEFAULT (0),
        NetAmount       DECIMAL(18,2) NOT NULL DEFAULT (0),
        PaymentStatus   NVARCHAR(20) NOT NULL DEFAULT ('PAID'),
        Status          NVARCHAR(20) NOT NULL DEFAULT ('COMPLETED'),
        Remarks         NVARCHAR(255) NULL,
        CreatedAt       DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CreatedBy       INT NOT NULL,
        CONSTRAINT FK_SalesOrders_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
        CONSTRAINT FK_SalesOrders_Users FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId)
    );
END
GO

IF OBJECT_ID('dbo.SalesOrderItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesOrderItems
    (
        SalesOrderItemId  INT IDENTITY(1,1) PRIMARY KEY,
        SalesOrderId      INT NOT NULL,
        ProductVariantId  INT NOT NULL,
        Quantity          INT NOT NULL,
        UnitPrice         DECIMAL(18,2) NOT NULL,
        LineTotal         DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_SOI_SalesOrders FOREIGN KEY (SalesOrderId) REFERENCES dbo.SalesOrders(SalesOrderId) ON DELETE CASCADE,
        CONSTRAINT FK_SOI_ProductVariants FOREIGN KEY (ProductVariantId) REFERENCES dbo.ProductVariants(ProductVariantId)
    );
END
GO

/* =====================================================================
   14. AUTH: REFRESH TOKENS / PASSWORD RESET TOKENS
   ===================================================================== */
IF OBJECT_ID('dbo.RefreshTokens', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens
    (
        RefreshTokenId        INT IDENTITY(1,1) PRIMARY KEY,
        UserId                INT NOT NULL,
        TokenHash             NVARCHAR(255) NOT NULL,
        ExpiresAt             DATETIME2 NOT NULL,
        CreatedAt             DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CreatedByIp           NVARCHAR(50) NULL,
        RevokedAt             DATETIME2 NULL,
        RevokedByIp           NVARCHAR(50) NULL,
        ReplacedByTokenHash   NVARCHAR(255) NULL,
        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
    );
END
GO

IF OBJECT_ID('dbo.PasswordResetTokens', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        ResetTokenId  INT IDENTITY(1,1) PRIMARY KEY,
        UserId        INT NOT NULL,
        TokenHash     NVARCHAR(255) NOT NULL,
        ExpiresAt     DATETIME2 NOT NULL,
        IsUsed        BIT NOT NULL DEFAULT (0),
        CreatedAt     DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_PRT_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
    );
END
GO

/* =====================================================================
   15. AUDIT LOG
   ===================================================================== */
IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs
    (
        AuditLogId  BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId      INT NULL,
        Username    NVARCHAR(50) NULL,
        Action      NVARCHAR(50) NOT NULL,
        TableName   NVARCHAR(100) NULL,
        RecordId    NVARCHAR(50) NULL,
        OldValues   NVARCHAR(MAX) NULL,
        NewValues   NVARCHAR(MAX) NULL,
        IPAddress   NVARCHAR(50) NULL,
        CreatedAt   DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME())
    );
END
GO

/* =====================================================================
   16. WEBSITE CONTENT PAGES
   ===================================================================== */
IF OBJECT_ID('dbo.ContentPages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContentPages
    (
        ContentPageId  INT IDENTITY(1,1) PRIMARY KEY,
        Title          NVARCHAR(200) NOT NULL,
        Slug           NVARCHAR(200) NOT NULL UNIQUE,
        Body           NVARCHAR(MAX) NULL,
        ImagePath      NVARCHAR(255) NULL,
        IsPublished    BIT NOT NULL DEFAULT (0),
        DisplayOrder   INT NOT NULL DEFAULT (0),
        CreatedAt      DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt      DATETIME2 NULL,
        CreatedBy      INT NULL,
        UpdatedBy      INT NULL
    );
END
GO

/* =====================================================================
   17. COLORS  (second variant dimension alongside Size - e.g. "Slim Fit
       Jeans" in Blue/Black, each further split by size, each with its
       own barcode/stock - see ProductVariants.ColorId below)
   ===================================================================== */
IF OBJECT_ID('dbo.Colors', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Colors
    (
        ColorId       INT IDENTITY(1,1) PRIMARY KEY,
        ColorName     NVARCHAR(50) NOT NULL UNIQUE,
        HexCode       NVARCHAR(10) NULL,
        DisplayOrder  INT NOT NULL DEFAULT (0),
        IsActive      BIT NOT NULL DEFAULT (1),
        CreatedAt     DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME())
    );
END
GO

-- Safe additive upgrade: give ProductVariants an optional ColorId. Nullable on purpose - shops that
-- don't sell in multiple colors can keep leaving it blank (a variant is then just Product + Size,
-- same as before this column existed).
IF OBJECT_ID('dbo.ProductVariants', 'U') IS NOT NULL AND COL_LENGTH('dbo.ProductVariants', 'ColorId') IS NULL
BEGIN
    ALTER TABLE dbo.ProductVariants ADD ColorId INT NULL;
END
GO

IF OBJECT_ID('dbo.ProductVariants', 'U') IS NOT NULL AND OBJECT_ID('dbo.Colors', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ProductVariants_Colors')
BEGIN
    ALTER TABLE dbo.ProductVariants ADD CONSTRAINT FK_ProductVariants_Colors FOREIGN KEY (ColorId) REFERENCES dbo.Colors(ColorId);
END
GO

-- Color is now part of what makes a variant unique, so widen the old Product+Size uniqueness to
-- Product+Size+Color (a NULL ColorId is still "no color" - SQL Server treats multiple NULLs in a
-- unique constraint as distinct from each other, which is fine: a colorless shop just never adds a
-- second row with the same size anyway).
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_ProductVariants_ProductSize')
BEGIN
    ALTER TABLE dbo.ProductVariants DROP CONSTRAINT UQ_ProductVariants_ProductSize;
END
GO

IF OBJECT_ID('dbo.ProductVariants', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_ProductVariants_ProductSizeColor')
BEGIN
    ALTER TABLE dbo.ProductVariants ADD CONSTRAINT UQ_ProductVariants_ProductSizeColor UNIQUE (ProductId, SizeId, ColorId);
END
GO

/* =====================================================================
   18. SUPPLIERS  ("Due" is computed on the fly from Purchases, not
       stored - SUM(TotalAmount - PaidAmount) across a supplier's
       purchases - so there's no separate ledger table to keep in sync)
   ===================================================================== */
IF OBJECT_ID('dbo.Suppliers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Suppliers
    (
        SupplierId    INT IDENTITY(1,1) PRIMARY KEY,
        SupplierName  NVARCHAR(150) NOT NULL,
        Phone         NVARCHAR(30) NULL,
        Address       NVARCHAR(255) NULL,
        IsActive      BIT NOT NULL DEFAULT (1),
        CreatedAt     DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedAt     DATETIME2 NULL,
        CreatedBy     INT NULL,
        UpdatedBy     INT NULL
    );
END
GO

/* =====================================================================
   19. PURCHASES  (Purchase Module - a purchase invoice from a supplier;
       receiving it increases stock the same way a sale decreases it -
       see StockTransactions.ReferenceType = 'PURCHASE')
   ===================================================================== */
IF OBJECT_ID('dbo.Purchases', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Purchases
    (
        PurchaseId          INT IDENTITY(1,1) PRIMARY KEY,
        PurchaseInvoiceNo   NVARCHAR(30) NOT NULL UNIQUE,
        SupplierId          INT NOT NULL,
        PurchaseDate        DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        TotalAmount         DECIMAL(18,2) NOT NULL DEFAULT (0),
        PaidAmount          DECIMAL(18,2) NOT NULL DEFAULT (0),
        Remarks             NVARCHAR(255) NULL,
        CreatedAt           DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT NOT NULL,
        CONSTRAINT FK_Purchases_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId),
        CONSTRAINT FK_Purchases_Users FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId)
    );
END
GO

IF OBJECT_ID('dbo.PurchaseItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseItems
    (
        PurchaseItemId    INT IDENTITY(1,1) PRIMARY KEY,
        PurchaseId        INT NOT NULL,
        ProductVariantId  INT NOT NULL,
        Quantity          INT NOT NULL,
        BuyingPrice       DECIMAL(18,2) NOT NULL,
        LineTotal         DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_PI_Purchases FOREIGN KEY (PurchaseId) REFERENCES dbo.Purchases(PurchaseId) ON DELETE CASCADE,
        CONSTRAINT FK_PI_ProductVariants FOREIGN KEY (ProductVariantId) REFERENCES dbo.ProductVariants(ProductVariantId)
    );
END
GO

/* =====================================================================
   20. EXPENSES  (Category is a plain constrained string, not a separate
       lookup table - the fixed list below covers a typical shop's needs
       and keeps this module simple; "Other" covers anything else)
   ===================================================================== */
IF OBJECT_ID('dbo.Expenses', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Expenses
    (
        ExpenseId     INT IDENTITY(1,1) PRIMARY KEY,
        ExpenseDate   DATE NOT NULL,
        Category      NVARCHAR(30) NOT NULL
            CONSTRAINT CK_Expenses_Category CHECK (Category IN (
                N'Shop Rent', N'Electricity', N'Salary', N'Internet',
                N'Packaging', N'Marketing', N'Courier', N'Other')),
        Amount        DECIMAL(18,2) NOT NULL,
        Remarks       NVARCHAR(255) NULL,
        CreatedAt     DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CreatedBy     INT NULL
    );
END
GO

-- Safe additive upgrade: how the customer paid (separate from PaymentStatus, which tracks whether
-- it's settled yet). A plain constrained string, same "keep it simple" approach as Expenses.Category.
IF OBJECT_ID('dbo.SalesOrders', 'U') IS NOT NULL AND COL_LENGTH('dbo.SalesOrders', 'PaymentMethod') IS NULL
BEGIN
    ALTER TABLE dbo.SalesOrders ADD PaymentMethod NVARCHAR(20) NOT NULL
        CONSTRAINT DF_SalesOrders_PaymentMethod DEFAULT (N'CASH')
        CONSTRAINT CK_SalesOrders_PaymentMethod CHECK (PaymentMethod IN (N'CASH', N'CARD', N'MOBILE_BANKING', N'DUE'));
END
GO

PRINT 'Schema check complete.';
GO

/* =====================================================================
   SEED DATA
   ===================================================================== */

-- Roles
IF NOT EXISTS (SELECT 1 FROM dbo.Roles)
BEGIN
    INSERT INTO dbo.Roles (RoleName, Description, IsSystemRole, IsActive) VALUES
    (N'SuperAdmin', N'Full unrestricted access to every module', 1, 1),
    (N'Admin',      N'Administrative access to setup and operations', 1, 1),
    (N'Manager',    N'Manage sales, stock and reports', 0, 1),
    (N'Staff',      N'Sales entry and dashboard only', 0, 1);
END
GO

-- Users: superadmin / admin, password for both = 12345 (BCrypt hash below)
IF NOT EXISTS (SELECT 1 FROM dbo.Users)
BEGIN
    DECLARE @SuperAdminRoleId INT = (SELECT RoleId FROM dbo.Roles WHERE RoleName = N'SuperAdmin');
    DECLARE @AdminRoleId INT = (SELECT RoleId FROM dbo.Roles WHERE RoleName = N'Admin');
    -- BCrypt hash of "12345"
    DECLARE @PasswordHash NVARCHAR(255) = N'$2b$11$td3WsJtTygkxeRlxJqQ5EetgLLN3HJ55YKQ4SpuP2H3GV681YL866';

    INSERT INTO dbo.Users (Username, Email, PasswordHash, FullName, RoleId, IsActive) VALUES
    (N'superadmin', N'superadmin@gmail.com', @PasswordHash, N'Super Administrator', @SuperAdminRoleId, 1),
    (N'admin',      N'admin@gmail.com', @PasswordHash, N'System Administrator', @AdminRoleId, 1);
END
GO

-- Sizes
IF NOT EXISTS (SELECT 1 FROM dbo.Sizes)
BEGIN
    INSERT INTO dbo.Sizes (SizeName, DisplayOrder, IsActive) VALUES
    (N'XS', 1, 1), (N'S', 2, 1), (N'M', 3, 1), (N'L', 4, 1), (N'XL', 5, 1), (N'XXL', 6, 1), (N'Free Size', 7, 1);
END
GO

-- Brands
IF NOT EXISTS (SELECT 1 FROM dbo.Brands)
BEGIN
    INSERT INTO dbo.Brands (BrandName, Description, IsActive) VALUES
    (N'Urban Edge', N'Everyday streetwear essentials', 1),
    (N'DenimCraft', N'Denim and casual wear', 1),
    (N'StyleHouse', N'Women''s fashion line', 1);
END
GO

-- Colors (Product Variant's second dimension, alongside Size)
IF NOT EXISTS (SELECT 1 FROM dbo.Colors)
BEGIN
    INSERT INTO dbo.Colors (ColorName, HexCode, DisplayOrder, IsActive) VALUES
    (N'Black', N'#000000', 1, 1),
    (N'White', N'#FFFFFF', 2, 1),
    (N'Grey', N'#808080', 3, 1),
    (N'Navy', N'#000080', 4, 1),
    (N'Blue', N'#0000FF', 5, 1),
    (N'Red', N'#FF0000', 6, 1),
    (N'Green', N'#008000', 7, 1),
    (N'Maroon', N'#800000', 8, 1);
END
GO

-- Menus (base set)
IF NOT EXISTS (SELECT 1 FROM dbo.Menus)
BEGIN
    INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive) VALUES
    (N'DASHBOARD',     N'Dashboard',        N'bi-speedometer2',  N'/Dashboard',      NULL, 1,  1),
    (N'SETUP',         N'Setup',            N'bi-collection',    N'/Setup',          NULL, 2,  1),
    (N'USERS',         N'Users',            N'bi-people',        N'/Users',          NULL, 3,  1),
    (N'ROLES',         N'Roles',            N'bi-shield-lock',   N'/Roles',          NULL, 4,  1),
    (N'MENUS',         N'Menus',            N'bi-list-nested',   N'/Menus',          NULL, 5,  1),
    (N'CATEGORIES',    N'Categories',       N'bi-tags',          N'/Categories',     NULL, 6,  1),
    (N'SUBCATEGORIES', N'Subcategories',    N'bi-tag',           N'/Subcategories',  NULL, 7,  1),
    (N'PRODUCTS',      N'Products',         N'bi-box-seam',      N'/Products',       NULL, 8,  1),
    (N'STOCK',         N'Stock',            N'bi-boxes',         N'/Stock',          NULL, 9,  1),
    (N'SALES',         N'Sales',            N'bi-cart-check',    N'/Sales',          NULL, 10, 1),
    (N'REPORTS',       N'Reports',          N'bi-graph-up',      N'/Reports',        NULL, 11, 1),
    (N'CONTENT',       N'Website Content',  N'bi-globe',         N'/Content',        NULL, 12, 1),
    (N'AUDITLOG',      N'Audit Log',        N'bi-journal-text',  N'/AuditLog',       NULL, 13, 1);
END
GO

-- Menu grouping: Admin / Catalog / Sales / Reports parent sections, plus the new
-- Brands/Sizes/Barcode Labels pages. Safe to run on an existing install too - no data
-- at risk here, only navigation metadata, and everything is guarded by IF NOT EXISTS.
IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'GROUP_ADMIN')
BEGIN
    INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive) VALUES
    (N'GROUP_CATALOG', N'Catalog', N'bi-box-seam',   NULL, NULL, 20, 1),
    (N'GROUP_SALES',   N'Sales',   N'bi-cart-check', NULL, NULL, 30, 1),
    (N'GROUP_REPORTS', N'Reports', N'bi-graph-up',   NULL, NULL, 40, 1),
    (N'GROUP_ADMIN',   N'Admin',   N'bi-gear',       NULL, NULL, 50, 1);

    DECLARE @GCatalog INT = (SELECT MenuId FROM dbo.Menus WHERE MenuKey = N'GROUP_CATALOG');
    DECLARE @GSales INT = (SELECT MenuId FROM dbo.Menus WHERE MenuKey = N'GROUP_SALES');
    DECLARE @GReports INT = (SELECT MenuId FROM dbo.Menus WHERE MenuKey = N'GROUP_REPORTS');
    DECLARE @GAdmin INT = (SELECT MenuId FROM dbo.Menus WHERE MenuKey = N'GROUP_ADMIN');

    UPDATE dbo.Menus SET ParentId = @GCatalog WHERE MenuKey IN (N'CATEGORIES', N'SUBCATEGORIES', N'PRODUCTS', N'STOCK');
    UPDATE dbo.Menus SET ParentId = @GSales   WHERE MenuKey IN (N'SALES');
    UPDATE dbo.Menus SET ParentId = @GReports WHERE MenuKey IN (N'REPORTS');
    UPDATE dbo.Menus SET ParentId = @GAdmin   WHERE MenuKey IN (N'USERS', N'ROLES', N'MENUS', N'CONTENT', N'AUDITLOG');

    IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'BRANDS')
        INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
        VALUES (N'BRANDS', N'Brands', N'bi-award', N'/Brands', @GCatalog, 5, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'SIZES')
        INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
        VALUES (N'SIZES', N'Sizes', N'bi-rulers', N'/Sizes', @GCatalog, 6, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'BARCODEGEN')
        INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
        VALUES (N'BARCODEGEN', N'Barcode Labels', N'bi-upc-scan', N'/BarcodeGenerator', @GCatalog, 9, 1);

    -- Grant the new menus the same access pattern as their siblings for each existing role
    INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanCreate, CanEdit, CanDelete)
    SELECT r.RoleId, m.MenuId,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin') THEN 1 ELSE 0 END,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin') THEN 1 ELSE 0 END,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin') THEN 1 ELSE 0 END,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin') THEN 1 ELSE 0 END
    FROM dbo.Roles r
    CROSS JOIN dbo.Menus m
    WHERE m.MenuKey IN (N'GROUP_CATALOG', N'GROUP_SALES', N'GROUP_REPORTS', N'GROUP_ADMIN', N'BRANDS', N'SIZES', N'BARCODEGEN')
      AND NOT EXISTS (SELECT 1 FROM dbo.RoleMenuPermissions p WHERE p.RoleId = r.RoleId AND p.MenuId = m.MenuId);

    -- A group header should be viewable by a role if it can view at least one of its children
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

-- Menu grouping (round 2): Purchase module (Suppliers/Purchases/Expenses) + Colors under Catalog.
-- Own outer guard (separate from the GROUP_ADMIN block above) so this still runs on an install that
-- already has GROUP_ADMIN from an earlier version of this script - it would otherwise be silently
-- skipped forever since that whole block only ever runs once.
IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'GROUP_PURCHASE')
BEGIN
    INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive) VALUES
    (N'GROUP_PURCHASE', N'Purchase', N'bi-truck', NULL, NULL, 25, 1);

    DECLARE @GPurchase INT = (SELECT MenuId FROM dbo.Menus WHERE MenuKey = N'GROUP_PURCHASE');
    DECLARE @GCatalog2 INT = (SELECT MenuId FROM dbo.Menus WHERE MenuKey = N'GROUP_CATALOG');
    DECLARE @GAdmin2 INT = (SELECT MenuId FROM dbo.Menus WHERE MenuKey = N'GROUP_ADMIN');

    IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'COLORS')
        INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
        VALUES (N'COLORS', N'Colors', N'bi-palette', N'/Colors', @GCatalog2, 7, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'SUPPLIERS')
        INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
        VALUES (N'SUPPLIERS', N'Suppliers', N'bi-people-fill', N'/Suppliers', @GPurchase, 1, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'PURCHASES')
        INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
        VALUES (N'PURCHASES', N'Purchases', N'bi-bag-plus', N'/Purchases', @GPurchase, 2, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Menus WHERE MenuKey = N'EXPENSES')
        INSERT INTO dbo.Menus (MenuKey, MenuName, Icon, Url, ParentId, DisplayOrder, IsActive)
        VALUES (N'EXPENSES', N'Expenses', N'bi-cash-stack', N'/Expenses', @GAdmin2, 20, 1);

    -- SuperAdmin/Admin get full access; Manager gets view/create/edit (matches how Manager is
    -- treated for Products/Stock/Sales above) since running purchases day-to-day is a manager task;
    -- Staff gets nothing (same "sales entry only" scope Staff already has everywhere else).
    INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanCreate, CanEdit, CanDelete)
    SELECT r.RoleId, m.MenuId,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin', N'Manager') THEN 1 ELSE 0 END,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin', N'Manager') THEN 1 ELSE 0 END,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin', N'Manager') THEN 1 ELSE 0 END,
        CASE WHEN r.RoleName IN (N'SuperAdmin', N'Admin') THEN 1 ELSE 0 END
    FROM dbo.Roles r
    CROSS JOIN dbo.Menus m
    WHERE m.MenuKey IN (N'GROUP_PURCHASE', N'COLORS', N'SUPPLIERS', N'PURCHASES', N'EXPENSES')
      AND NOT EXISTS (SELECT 1 FROM dbo.RoleMenuPermissions p WHERE p.RoleId = r.RoleId AND p.MenuId = m.MenuId);

    -- Same "group header viewable if any child is viewable" rule as the first grouping block.
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

-- Role Menu Permissions (base set, for a fresh install before grouping runs above)
IF NOT EXISTS (SELECT 1 FROM dbo.RoleMenuPermissions)
BEGIN
    DECLARE @SuperAdmin INT = (SELECT RoleId FROM dbo.Roles WHERE RoleName = N'SuperAdmin');
    DECLARE @Admin INT = (SELECT RoleId FROM dbo.Roles WHERE RoleName = N'Admin');
    DECLARE @Manager INT = (SELECT RoleId FROM dbo.Roles WHERE RoleName = N'Manager');
    DECLARE @Staff INT = (SELECT RoleId FROM dbo.Roles WHERE RoleName = N'Staff');

    INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanCreate, CanEdit, CanDelete)
    SELECT @SuperAdmin, MenuId, 1, 1, 1, 1 FROM dbo.Menus;

    INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanCreate, CanEdit, CanDelete)
    SELECT @Admin, MenuId, 1, 1, 1, 1 FROM dbo.Menus;

    INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanCreate, CanEdit, CanDelete)
    SELECT @Manager, MenuId,
        CASE WHEN MenuKey IN (N'DASHBOARD', N'CATEGORIES', N'SUBCATEGORIES', N'PRODUCTS', N'STOCK', N'SALES', N'REPORTS') THEN 1 ELSE 0 END,
        CASE WHEN MenuKey IN (N'CATEGORIES', N'SUBCATEGORIES', N'PRODUCTS', N'STOCK', N'SALES') THEN 1 ELSE 0 END,
        CASE WHEN MenuKey IN (N'CATEGORIES', N'SUBCATEGORIES', N'PRODUCTS', N'STOCK', N'SALES') THEN 1 ELSE 0 END,
        0
    FROM dbo.Menus;

    INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanCreate, CanEdit, CanDelete)
    SELECT @Staff, MenuId,
       
        CASE WHEN MenuKey IN (N'DASHBOARD', N'SALES') THEN 1 ELSE 0 END,
        CASE WHEN MenuKey IN (N'SALES') THEN 1 ELSE 0 END,
        0, 0
    FROM dbo.Menus;
END
GO

-- Sample fashion catalog: Categories / Subcategories / Products / Variants (Size+Barcode) / Price / Stock
IF NOT EXISTS (SELECT 1 FROM dbo.Categories)
BEGIN
    INSERT INTO dbo.Categories (CategoryName, Description, IsActive) VALUES
    (N'Men''s Wear', N'Clothing for men', 1),
    (N'Women''s Wear', N'Clothing for women', 1),
    (N'Footwear', N'Shoes and sneakers', 1);

    DECLARE @Men INT = (SELECT CategoryId FROM dbo.Categories WHERE CategoryName = N'Men''s Wear');
    DECLARE @Women INT = (SELECT CategoryId FROM dbo.Categories WHERE CategoryName = N'Women''s Wear');
    DECLARE @Foot INT = (SELECT CategoryId FROM dbo.Categories WHERE CategoryName = N'Footwear');

    INSERT INTO dbo.Subcategories (CategoryId, SubcategoryName, Description, IsActive) VALUES
    (@Men, N'T-Shirts', N'Casual t-shirts', 1),
    (@Men, N'Jeans', N'Denim trousers', 1),
    (@Women, N'Dresses', N'Casual and formal dresses', 1),
    (@Foot, N'Sneakers', N'Casual sneakers', 1);

    DECLARE @SubTshirt INT = (SELECT SubcategoryId FROM dbo.Subcategories WHERE SubcategoryName = N'T-Shirts');
    DECLARE @SubJeans INT = (SELECT SubcategoryId FROM dbo.Subcategories WHERE SubcategoryName = N'Jeans');
    DECLARE @SubDress INT = (SELECT SubcategoryId FROM dbo.Subcategories WHERE SubcategoryName = N'Dresses');
    DECLARE @SubSneaker INT = (SELECT SubcategoryId FROM dbo.Subcategories WHERE SubcategoryName = N'Sneakers');

    DECLARE @BrandUrban INT = (SELECT BrandId FROM dbo.Brands WHERE BrandName = N'Urban Edge');
    DECLARE @BrandDenim INT = (SELECT BrandId FROM dbo.Brands WHERE BrandName = N'DenimCraft');
    DECLARE @BrandStyle INT = (SELECT BrandId FROM dbo.Brands WHERE BrandName = N'StyleHouse');

    INSERT INTO dbo.Products (ProductCode, ProductName, CategoryId, SubcategoryId, BrandId, Unit, Description, ReorderLevel, IsShowOnWebsite, ShowPriceOnWebsite, IsActive) VALUES
    (N'PRD-0001', N'Classic Crew T-Shirt', @Men, @SubTshirt, @BrandUrban, N'pcs', N'Soft cotton crew-neck t-shirt', 10, 1, 1, 1),
    (N'PRD-0002', N'Slim Fit Jeans', @Men, @SubJeans, @BrandDenim, N'pcs', N'Stretch denim, slim fit', 5, 1, 1, 1),
    (N'PRD-0003', N'Summer Floral Dress', @Women, @SubDress, @BrandStyle, N'pcs', N'Lightweight floral print dress', 5, 1, 0, 1),
    (N'PRD-0004', N'Canvas Sneakers', @Foot, @SubSneaker, @BrandUrban, N'pair', N'Everyday canvas sneakers', 5, 0, 1, 1);

    INSERT INTO dbo.Price (ProductId, CostPrice, SellingPrice, IsActive)
    SELECT ProductId,
        CASE ProductCode WHEN N'PRD-0001' THEN 8.00 WHEN N'PRD-0002' THEN 22.00 WHEN N'PRD-0003' THEN 18.00 ELSE 15.00 END,
        CASE ProductCode WHEN N'PRD-0001' THEN 19.99 WHEN N'PRD-0002' THEN 49.99 WHEN N'PRD-0003' THEN 39.99 ELSE 34.99 END,
        1
    FROM dbo.Products;

    -- One variant (Size + unique Barcode) per size the product is stocked in, with starting stock
    DECLARE @SizeS INT = (SELECT SizeId FROM dbo.Sizes WHERE SizeName = N'S');
    DECLARE @SizeM INT = (SELECT SizeId FROM dbo.Sizes WHERE SizeName = N'M');
    DECLARE @SizeL INT = (SELECT SizeId FROM dbo.Sizes WHERE SizeName = N'L');
    DECLARE @SizeXL INT = (SELECT SizeId FROM dbo.Sizes WHERE SizeName = N'XL');
    DECLARE @SizeFree INT = (SELECT SizeId FROM dbo.Sizes WHERE SizeName = N'Free Size');

    DECLARE @PTshirt INT = (SELECT ProductId FROM dbo.Products WHERE ProductCode = N'PRD-0001');
    DECLARE @PJeans INT = (SELECT ProductId FROM dbo.Products WHERE ProductCode = N'PRD-0002');
    DECLARE @PDress INT = (SELECT ProductId FROM dbo.Products WHERE ProductCode = N'PRD-0003');
    DECLARE @PSneaker INT = (SELECT ProductId FROM dbo.Products WHERE ProductCode = N'PRD-0004');

    INSERT INTO dbo.ProductVariants (ProductId, SizeId, Barcode, SKU, ReorderLevel, IsActive) VALUES
    (@PTshirt, @SizeS,  N'DIN000000001', N'PRD-0001-S',  10, 1),
    (@PTshirt, @SizeM,  N'DIN000000002', N'PRD-0001-M',  10, 1),
    (@PTshirt, @SizeL,  N'DIN000000003', N'PRD-0001-L',  10, 1),
    (@PJeans,  @SizeM,  N'DIN000000004', N'PRD-0002-M',  5,  1),
    (@PJeans,  @SizeL,  N'DIN000000005', N'PRD-0002-L',  5,  1),
    (@PDress,  @SizeS,  N'DIN000000006', N'PRD-0003-S',  5,  1),
    (@PDress,  @SizeM,  N'DIN000000007', N'PRD-0003-M',  5,  1),
    (@PSneaker, @SizeFree, N'DIN000000008', N'PRD-0004-FS', 5, 1);

    INSERT INTO dbo.Stock (ProductVariantId, QuantityOnHand)
    SELECT ProductVariantId,
        CASE Barcode
            WHEN N'DIN000000001' THEN 20 WHEN N'DIN000000002' THEN 25 WHEN N'DIN000000003' THEN 15
            WHEN N'DIN000000004' THEN 12 WHEN N'DIN000000005' THEN 10
            WHEN N'DIN000000006' THEN 8  WHEN N'DIN000000007' THEN 8
            ELSE 30
        END
    FROM dbo.ProductVariants;
END
GO

-- Upgrade path: if Products already existed (from an older run of this script within the SAME
-- new-schema version) but has no variants yet, give every product a default "Free Size" variant
-- so Stock/Sales keep working without requiring a full manual re-entry.
IF EXISTS (SELECT 1 FROM dbo.Products) AND NOT EXISTS (SELECT 1 FROM dbo.ProductVariants)
BEGIN
    DECLARE @DefaultSizeId INT = (SELECT SizeId FROM dbo.Sizes WHERE SizeName = N'Free Size');
    IF @DefaultSizeId IS NOT NULL
    BEGIN
        INSERT INTO dbo.ProductVariants (ProductId, SizeId, Barcode, SKU, ReorderLevel, IsActive, CreatedAt)
        SELECT p.ProductId, @DefaultSizeId, p.ProductCode, p.ProductCode, p.ReorderLevel, p.IsActive, SYSUTCDATETIME()
        FROM dbo.Products p
        WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductVariants v WHERE v.ProductId = p.ProductId);

        INSERT INTO dbo.Stock (ProductVariantId, QuantityOnHand)
        SELECT v.ProductVariantId, 0
        FROM dbo.ProductVariants v
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Stock s WHERE s.ProductVariantId = v.ProductVariantId);
    END
END
GO

-- Sample home page content
IF NOT EXISTS (SELECT 1 FROM dbo.ContentPages)
BEGIN
    INSERT INTO dbo.ContentPages (Title, Slug, Body, IsPublished, DisplayOrder) VALUES
    (N'Welcome', N'home', N'<h2>Welcome to DInventory</h2><p>Your fashion house''s inventory and sales management solution.</p>', 1, 1),
    (N'About Us', N'about-us', N'<h2>About Us</h2><p>DInventory helps fashion retailers manage products, sizes, stock and sales with ease.</p>', 1, 2);
END
GO

PRINT 'DInventoryDB schema and seed data created successfully.';
PRINT 'Login: superadmin / 12345  or  admin / 12345';
