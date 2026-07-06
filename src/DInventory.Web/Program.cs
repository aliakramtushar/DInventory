using System.Text;
using System.Threading.RateLimiting;
using DInventory.Application;
using DInventory.Application.Common.Models;
using DInventory.Infrastructure;
using DInventory.Infrastructure.Persistence;
using DInventory.Web.Common;
using DInventory.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// MVC + Razor views
builder.Services.AddControllersWithViews();

// Distributed (in-memory) session store, used only to persist a SuperAdmin's globally selected
// company (see ICompanyContextService) across pages for the rest of their login. Non-SuperAdmin
// users never touch this - they're always pinned to their own company from the auth claims.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "dinv_session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // "Always" only in real Production, since UseHttpsRedirection below guarantees the site is only
    // ever served over HTTPS there. Development AND any other named environment (e.g. a local IIS
    // "Staging" deployment with no HTTPS binding - see FolderProfile.pubxml) get SameAsRequest so the
    // cookie still works over plain HTTP.
    options.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

// Application layer (use cases / business rules) + Infrastructure layer (Dapper repos, JWT, email, hashing)
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, enforceLoginLockout: builder.Environment.IsProduction());

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

// Refuse to start in Production with a missing/placeholder/too-short JWT signing key - this key is
// what proves a bearer token is genuinely ours, so shipping the checked-in placeholder (or any short
// key) to a live server would let anyone forge a valid admin session token. Keyed off IsProduction()
// rather than "not Development" so a local IIS test/staging deployment (ASPNETCORE_ENVIRONMENT=Staging,
// see FolderProfile.pubxml) isn't forced to jump through the same hoop as a real public deployment.
if (builder.Environment.IsProduction())
{
    const string placeholderJwtKey = "CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS_LONG!";
    if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key == placeholderJwtKey || jwtSettings.Key.Length < 32)
    {
        throw new InvalidOperationException(
            "Refusing to start: Jwt:Key is missing, still the placeholder, or shorter than 32 characters. " +
            "Set a real random secret via the Jwt__Key environment variable (or a secrets manager / " +
            "'dotnet user-secrets') before deploying - do not put production secrets in appsettings.json.");
    }
}

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
        options.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
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

// --- Rate limiting -----------------------------------------------------------
// Global per-IP cap so no single client can hammer the app; a much stricter "login" policy on top
// of that specifically throttles the brute-force surface at /Account/Login (in addition to the
// per-account lockout in AuthService/ILoginAttemptGuard - that one keys on username, this one keys
// on IP, so together they cover both a single attacker guessing many passwords and a botnet
// guessing one password across many IPs).
var enforceRateLimits = builder.Environment.IsProduction();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Outside Production this resolves to GetNoLimiter (always-allow) for every partition, rather
    // than skipping registration entirely - the "login" policy name below must still exist or
    // [EnableRateLimiting("login")] would throw at request time even when we don't want it enforced.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return enforceRateLimits
            ? RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            })
            : RateLimitPartition.GetNoLimiter(key);
    });

    options.AddPolicy("login", context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return enforceRateLimits
            ? RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            })
            : RateLimitPartition.GetNoLimiter(key);
    });
});

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
}

// HSTS and forced HTTPS are strictly a Production concern - a local IIS "Staging" deployment (see
// FolderProfile.pubxml) commonly has no HTTPS binding at all, and both of these are no-ops/warnings
// at best there anyway. Keeping them Production-only avoids noisy "failed to determine https port"
// log entries on every local request.
if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "DInventory API v1");
    options.RoutePrefix = "swagger";
});

app.UseRouting();

app.UseRateLimiter();

// Must come after UseRouting and before anything that reads/writes ICompanyContextService's
// session-backed selection (controllers, view components) - i.e. before auth/endpoints.
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
