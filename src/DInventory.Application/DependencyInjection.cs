using DInventory.Application.Audit;
using DInventory.Application.Auth;
using DInventory.Application.Barcoding;
using DInventory.Application.Catalog;
using DInventory.Application.Content;
using DInventory.Application.Customers;
using DInventory.Application.Dashboard;
using DInventory.Application.Expenses;
using DInventory.Application.Inventory;
using DInventory.Application.Menus;
using DInventory.Application.Purchasing;
using DInventory.Application.Reports;
using DInventory.Application.Roles;
using DInventory.Application.Sales;
using DInventory.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace DInventory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISubcategoryService, SubcategoryService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<ISizeService, SizeService>();
        services.AddScoped<IColorService, ColorService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductVariantService, ProductVariantService>();
        services.AddScoped<IBarcodeNumberGenerator, BarcodeNumberGenerator>();
        services.AddScoped<IGeneratedBarcodeLabelService, GeneratedBarcodeLabelService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ILoyaltyService, LoyaltyService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<ISalesReturnService, SalesReturnService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IPurchaseReturnService, PurchaseReturnService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IContentPageService, ContentPageService>();

        return services;
    }
}
