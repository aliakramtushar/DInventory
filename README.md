# DInventory — Inventory + Sales Management System

ASP.NET Core 8 MVC (Razor/.cshtml) + Dapper + SQL Server LocalDB. Clean Architecture, full sales/purchase workflow with returns and discounts, a configurable customer loyalty program, JWT auth with refresh/revoke, role + menu-level permissions, dashboard, reporting, and a small public website with editable content.

## Features

- **Products & stock** — categories, subcategories, brands, colors/sizes, product variants, barcode generation, low-stock alerts, and a stock ledger where every change is recorded as a transaction (balance is always computed, never a mutated running total).
- **Sales** — cart-style checkout, per-line discount (percent or fixed amount) plus an overall bill discount, walk-in or registered customers, multiple payment methods, printable invoices.
- **Purchases** — supplier-linked purchase orders that receive stock in automatically.
- **Sale Returns / Purchase Returns** — always linked back to the original invoice/PO, partial or full returns per line item, stock adjusts automatically, and the net amount owed is reduced accordingly.
- **Customers & Loyalty** — customer records (name, address, email, mobile) with order history; a configurable loyalty program (points earned per amount spent, point value on redeem — no hardcoded rates, editable from Loyalty Settings); points can be redeemed as a bill discount at checkout; full earn/redeem/adjust ledger with automatic reversal if a sale is cancelled.
- **Expenses** — expense tracking that feeds into the dashboard's cash-in-hand figure.
- **Dashboard & Reports** — today/week/month/year sales, low-stock count, Chart.js trend charts (daily/weekly/monthly/yearly), Sales/Stock reports with CSV export.
- **Roles & permissions** — database-driven menu + per-role permission system (View/Create/Edit/Delete), enforced server-side on every controller action.
- **Auth** — JWT access + refresh tokens with cookie auth for the MVC pages, session revoke and admin-triggered force-logout.
- **Public website** — a small content-managed storefront alongside the admin app.
- **Pagination** — every admin list view (Products, Sales, Purchases, Customers, Suppliers, Returns, Expenses, Audit Log, etc.) is paginated and searchable via a shared `PagedResult<T>`/`PagedRequest` and a shared pagination partial view.

## 1. Prerequisites

- Windows with **.NET 8 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/8.0))
- **SQL Server LocalDB** (installed with Visual Studio, or the standalone "SQL Server Express LocalDB" package)
- Visual Studio 2022 (17.8+) or VS Code — optional, `dotnet` CLI is enough

## 2. Run it

No manual database step is required — the app creates `DInventoryDB` on `(localdb)\MSSQLLocalDB` and seeds it automatically on first run, using `database/Database.sql` (also copied to `src/DInventory.Web/App_Data/Database.sql`).

```
cd src/DInventory.Web
dotnet restore
dotnet run
```

Open **http://localhost:5080**. Log in with:

| Username     | Password | Role       |
|--------------|----------|------------|
| `superadmin` | `12345`  | SuperAdmin |
| `admin`      | `12345`  | Admin      |

If you'd rather run the SQL script yourself (e.g. against a different server), open `database/Database.sql` in SSMS/Azure Data Studio and execute it, then update `ConnectionStrings:DefaultConnection` in `src/DInventory.Web/appsettings.json`.

## 3. Project layout (Clean Architecture)

```
src/
  DInventory.Domain          Entities & enums only, no dependencies
  DInventory.Application     Interfaces, DTOs, business logic (services)
  DInventory.Infrastructure  Dapper repositories, JWT, BCrypt hashing, SMTP email
  DInventory.Web             ASP.NET Core MVC: controllers, Razor views, wwwroot
database/
  Database.sql                Full schema + seed data (idempotent, safe to re-run)
```

Adding a new module (e.g. "Suppliers") means: add the entity to `Domain`, an interface + service to `Application`, a Dapper repository to `Infrastructure`, register it in the two `DependencyInjection.cs` files, then add a controller + views to `Web`. Add a row to the `Menus` table (and grant it in `RoleMenuPermissions`) to make it show up in the sidebar automatically.

## 4. Authentication

- Login issues a short-lived **JWT access token** + a long-lived **refresh token** (hashed and stored in `RefreshTokens`).
- The MVC pages use ASP.NET Core **cookie auth** (issued at the same time as the JWT) so `[Authorize]` and tag helpers work normally.
- The JWT is also stored in a cookie (`dinv_access_token`, non-HttpOnly) and used as a real `Authorization: Bearer` header by `site.js` (`dinvFetch`) when calling the small JSON API under `/api/*` (e.g. the dashboard chart data) — that endpoint is secured independently via `JwtBearer` auth.
- `site.js` silently rotates the access token via `POST /api/auth/refresh` every 20 minutes and on any 401.
- Logout revokes the refresh token ("cancel token"). Admins can also force-revoke *all* of a user's active sessions from **Setup → Users** (the "force logout" button).

## 5. Roles & permissions

- `SuperAdmin` and `Admin` have full access to everything, including the **Setup** hub (`/Setup`) which lists every configuration page (Users, Roles, Menus, Categories, Subcategories, Products, Stock, Content, Audit Log, Reports).
- Every other role's access is driven entirely by the `RoleMenuPermissions` table (View/Create/Edit/Delete per menu) — edit it from **Roles → Permissions**. Two example roles (`Manager`, `Staff`) are seeded with reduced access.
- `PermissionAuthorizeAttribute` enforces this server-side on every controller action beyond Users/Roles/Menus (which are hard-restricted to SuperAdmin/Admin).

## 6. Email (password reset)

`Email:SmtpHost` is empty by default — in that case, "sent" emails (password reset links) are written as `.html` files to `src/DInventory.Web/App_Data/outbox/` instead of actually sending, so you can still test the flow without SMTP credentials. Fill in `Email:SmtpHost` / `SmtpUser` / `SmtpPassword` in `appsettings.json` (or user-secrets) to send real email — Gmail SMTP (`smtp.gmail.com:587` with an App Password) works fine.

## 7. Dashboard & reports

- Dashboard (`/Dashboard`) shows today/week/month/year sales, low-stock count, and a Chart.js trend chart switchable between daily/weekly/monthly/yearly.
- **Reports → Sales / Stock** (`/Reports/Sales`, `/Reports/Stock`) support CSV export.

## 8. API docs (Swagger)

The JWT-secured JSON endpoints (`/api/dashboard/trend`, `/api/auth/refresh`) are documented at **http://localhost:5080/swagger**. The MVC/Razor page routes are intentionally excluded from the doc. Click **Authorize** and paste an access token (log in via the UI first, then copy the `dinv_access_token` cookie value from your browser's dev tools) to try the endpoints directly.