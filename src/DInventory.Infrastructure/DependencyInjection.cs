using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Infrastructure.Auth;
using DInventory.Infrastructure.Common;
using DInventory.Infrastructure.Email;
using DInventory.Infrastructure.Persistence;
using DInventory.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DInventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));

        services.AddHttpContextAccessor();

        services.AddSingleton<IDbConnectionFactory, DapperDbConnectionFactory>();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICompanyContextService, CompanyContextService>();
        services.AddScoped<IBusinessUnitContextService, BusinessUnitContextService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IEmailService, SmtpEmailService>();

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IBusinessUnitRepository, BusinessUnitRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IRoleMenuPermissionRepository, RoleMenuPermissionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ISubcategoryRepository, SubcategoryRepository>();
        services.AddScoped<IBrandRepository, BrandRepository>();
        services.AddScoped<ISizeRepository, SizeRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductVariantRepository, ProductVariantRepository>();
        services.AddScoped<IGeneratedBarcodeLabelRepository, GeneratedBarcodeLabelRepository>();
        services.AddScoped<IPriceRepository, PriceRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ILoyaltyRepository, LoyaltyRepository>();
        services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
        services.AddScoped<ISalesReturnRepository, SalesReturnRepository>();
        services.AddScoped<IColorRepository, ColorRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<IPurchaseReturnRepository, PurchaseReturnRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IContentPageRepository, ContentPageRepository>();

        return services;
    }
}
