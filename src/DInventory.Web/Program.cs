using System.Text;
using DInventory.Application;
using DInventory.Application.Common.Models;
using DInventory.Infrastructure;
using DInventory.Infrastructure.Persistence;
using DInventory.Web.Common;
using DInventory.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// MVC + Razor views
builder.Services.AddControllersWithViews();

// Application layer (use cases / business rules) + Infrastructure layer (Dapper repos, JWT, email, hashing)
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// --- Swagger / OpenAPI ------------------------------------------------------
// Only documents the JWT-secured /api/* endpoints (dashboard trend data, token refresh) -
// the MVC/Razor page routes are intentionally excluded to keep the doc focused and usable.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DInventory API",
        Version = "v1",
        Description = "JWT-secured JSON endpoints backing the DInventory dashboard/reports. " +
                       "The full application is server-rendered MVC (Razor) and is not documented here."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT access token (no \"Bearer \" prefix needed) you get back from POST /api/auth/refresh, " +
                      "or copy the 'dinv_access_token' cookie value after logging in via the UI."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });

    // Only include actions under /api/ - hides the ~80 MVC page routes from the Swagger doc.
    options.DocInclusionPredicate((_, apiDescription) => (apiDescription.RelativePath ?? string.Empty).StartsWith("api/"));
});

// --- Authentication -------------------------------------------------------
// Cookie scheme drives the MVC/Razor pages (so [Authorize] + tag helpers work normally).
// JWT Bearer secures the small JSON API used by the dashboard charts and can be swapped in
// for a future mobile/SPA client without touching the Razor pages.
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.Name = "dinv_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Fallback: read the access token from the (non-HttpOnly) cookie when no Authorization
        // header is present, so the dashboard API keeps working even before site.js attaches one.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token) &&
                    context.Request.Cookies.TryGetValue(AuthCookies.AccessToken, out var cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Ensure DInventoryDB (schema + seed data) exists so the app is runnable immediately after clone.
// Watch the console output for "Database check complete" vs an "Automatic database setup failed" error -
// if it failed here, every page will fail below with a generic error since no table exists yet.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
    DbInitializer.EnsureDatabaseCreated(app.Configuration, app.Environment.ContentRootPath, logger);
}

// In Development, show the full exception + stack trace in the browser instead of the friendly
// error page, so problems (like the DB not being reachable) are easy to diagnose while building.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Global error handler - must be the first middleware so it wraps everything else.
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseHsts();
}

app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "DInventory API v1");
    options.RoutePrefix = "swagger";
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
