# InventoryPro: stock intelligence for small businesses

Small shops, kitchens and clinics lose money in two opposite ways: they **run out of fast sellers**, and they **tie up cash in stock that never sells**. Most inventory tools only *store* quantities. InventoryPro also answers the question the owner actually has every morning:

> **What should I reorder today, and how much?**

It combines a conventional inventory and order system (products, customers, orders, role-based access) with a **reorder planner** that turns recent sales into days of cover, demand-based reorder points, suggested order quantities, and a count of how much cash is sitting in dead or overstocked items.

Built with **ASP.NET Core 8 + EF Core** (layered architecture) and a **React + TypeScript** dashboard.

## What it does

**Decision support (the differentiator)**
- **Reorder planner:** ranks products by urgency (out of stock → critical → reorder soon), shows days of cover and a suggested order quantity, and lets you tune sales history, supplier lead time and safety buffer.
- **Slow-stock detection:** flags products that haven't sold for 60+ days and products with more than 90 days of cover, and totals the cash tied up in them.
- **Export:** download the purchase list as CSV (formula-injection safe) or copy a ready-to-paste message for a supplier on WhatsApp or SMS.
- **Dashboard:** stock value, items needing reorder, slow-stock value, a 30-day revenue chart and recent orders, all computed server-side.

**Core inventory and orders**
- Products with search, category filter, sorting, pagination, stock adjustments with an audit reason, and soft delete for products that have order history.
- Orders with multi-line stock validation, transactional creation, enforced status workflow (Pending → Confirmed → Shipped → Delivered; cancelling returns stock).
- Customers and categories, with role-based access (Admin, Staff).

## How the reorder planner works

All rules live in one pure, database-free function (`StockForecastCalculator`) so they are unit-tested in isolation.

| Quantity | Formula |
| --- | --- |
| Daily demand | units sold in the sales window ÷ window days (cancelled orders ignored) |
| Days of cover | stock on hand ÷ daily demand |
| Reorder point | daily demand × (lead time + safety days), never below the product's own reorder level |
| Order quantity | refill to daily demand × (lead time + safety + 14 days), minus stock on hand |
| **Critical** | days of cover is shorter than the supplier lead time (it will run out before a new order arrives) |
| **Not selling** | in stock, older than 60 days, zero sales in 60 days (never suggested for reorder) |

Defaults (30-day window, 7-day lead time, 3-day buffer, 14-day coverage, 60-day dead-stock, 90-day overstock) are configurable in `appsettings.json` under `Insights`, and the first three can be changed per request.

> **Known simplification:** the data model has no *cost price*, so stock value is shown **at selling price**. Adding a cost price is the natural next step for true working-capital figures.

## Technology

| Area | Technologies |
| --- | --- |
| Backend | C#, ASP.NET Core 8 Web API, EF Core, LINQ |
| Architecture | Domain, Application, Infrastructure, API layers; Repository + Unit of Work |
| Database | SQLite for development; SQL Server supported (provider-specific migrations) |
| Auth | ASP.NET Core Identity, JWT, role-based authorization, account lockout, rate-limited auth endpoints |
| Validation / mapping | FluentValidation, AutoMapper |
| Frontend | React 19, TypeScript (strict), Vite, React Router, Recharts |
| Testing | xUnit (backend), Vitest + Testing Library + Playwright (frontend) |
| Delivery | GitHub Actions CI for both stacks, Swagger/OpenAPI, health endpoints |

## Engineering notes

- **No overselling:** stock is changed with conditional database updates inside a transaction, so concurrent orders cannot drive stock negative and a failed line rolls back the whole order.
- **Safe status changes:** order status transitions are validated server-side; cancelling restores stock in the same transaction.
- **Query design:** filtering, sorting and pagination are composed in LINQ and run in the database. On SQLite, decimal columns are stored as `double` so price sorting and filtering translate (SQL Server uses `decimal(18,2)`).
- **Consistent errors:** API failures return `{ "status": 400, "message": "...", "errors": { ... } }`; model validation, authentication/authorization, rate limits, readiness failures and domain errors use the same shape. Unexpected errors are logged and never expose internals.
- **Security defaults:** the app refuses to start without a JWT key and a CORS allow-list; public self-registration is enabled only for local Development and remains **off in production** (new accounts are Staff, who can read customer data); auth endpoints are rate-limited and accounts lock after repeated failures.
- **Frontend:** typed API layer, route-level code splitting, stale-response protection, accessible `<dialog>` modals, and role-aware UI (admin-only actions are hidden for Staff; the API still enforces them).
- **Bounded reads:** every inventory list is paged with a server-side cap; `/api/products/low-stock` uses the same `{ items, pageNumber, pageSize, totalCount, ... }` envelope as other lists.
- **Request diagnostics:** Information-level request logs include method, path, status, duration, and correlation ID. EF SQL command logging is Warning by default. Local benchmark query-count headers are disabled unless explicitly enabled in Development.

## API endpoints

| Method | Route | Access | Purpose |
| --- | --- | --- | --- |
| `GET` | `/api/auth/registration-status` | Public, rate-limited | Report whether public Staff registration is enabled |
| `POST` | `/api/auth/login` | Public, rate-limited | Authenticate and return a JWT |
| `POST` | `/api/auth/register` | Public when enabled; otherwise Admin; rate-limited | Create a Staff account |
| `GET` | `/api/users` | Admin | Paged team directory |
| `GET` | `/api/users/{id}` | Admin | Read one team member |
| `POST` | `/api/users` | Admin | Create a Staff or Admin account |
| `PATCH` | `/api/users/{id}/role` | Admin | Change another user's role |
| `PATCH` | `/api/users/{id}/active` | Admin | Deactivate/reactivate another user and clear lockout on activation |
| `GET` | `/api/products` | Admin, Staff | Search, filter, sort, and page products |
| `GET` | `/api/products/{id}` | Admin, Staff | Read one product |
| `GET` | `/api/products/low-stock` | Admin, Staff | Paged reorder-level list |
| `POST`, `PUT`, `PATCH`, `DELETE` | `/api/products...` | Admin | Manage products and adjust stock |
| `GET` | `/api/categories...` | Admin, Staff | Read categories |
| `POST`, `DELETE` | `/api/categories...` | Admin | Manage categories |
| `GET` | `/api/customers...` | Admin, Staff | Read paged customers |
| `POST` | `/api/customers` | Admin, Staff | Create a customer |
| `GET` | `/api/orders...` | Admin, Staff | Search, filter by status, sort, and page orders; read details |
| `POST`, `PATCH` | `/api/orders...` | Admin, Staff | Create orders and transition status |
| `GET` | `/api/insights/summary` | Admin, Staff | Inventory and sales summary |
| `GET` | `/api/insights/reorder-suggestions` | Admin, Staff | Reorder planner results |
| `GET` | `/api/insights/sales-trend` | Admin, Staff | Bounded daily sales history |

Validation and API failures use `{ "status": number, "message": string, "errors": { "field": ["..."] } }`. See [api.http](./api.http) for request examples.

Administrators can manage accounts from the **Team** page. Account roles are limited to Admin and Staff, the password policy matches Identity (12+ characters with uppercase, lowercase, a digit, and a symbol), and administrators cannot change their own role or deactivate their own account. Deactivation and role changes invalidate existing JWT sessions; reactivation clears Identity lockout state. Public registration is disabled by default and in production.

## Phase 2 performance measurements

Measured in Release against the same seeded SQLite database (2,000 benchmark products, 500 customers, 4,000 orders, and 20,000 order lines): 5 warmups and 30 requests per endpoint. Latencies are medians; EF counts are average database commands per request. Order-list projection and the additive `Orders(OrderDate)`/`Orders(Status)` indexes were retained because the combined changes improved the measured order and insight paths. List endpoint responses remain bounded.

| Endpoint | Before median / P95 (ms) | After median / P95 (ms) | EF commands before / after |
| --- | ---: | ---: | ---: |
| Products list | 6.0 / 16.3 | 6.5 / 9.4 | 2.0 / 2.0 |
| Products search | 7.6 / 9.4 | 7.6 / 10.3 | 2.0 / 2.0 |
| Products sort | 5.6 / 7.2 | 4.6 / 6.4 | 2.0 / 2.0 |
| Products low-stock | 6.8 / 9.9 | 4.5 / 5.4 | 1.0 / 2.0 |
| Orders list | 55.8 / 68.8 | 26.5 / 30.8 | 2.0 / 2.0 |
| Customers list | 5.2 / 6.6 | 4.1 / 4.7 | 2.0 / 2.0 |
| Insights summary | 39.5 / 62.7 | 33.7 / 41.4 | 4.0 / 4.0 |
| Reorder suggestions | 40.2 / 58.5 | 32.5 / 42.7 | 3.0 / 3.0 |
| Sales trend | 10.3 / 13.9 | 8.1 / 9.0 | 1.0 / 1.0 |

Low-stock measurements are directional rather than a strict before/after comparison because the earlier endpoint returned an unpaged collection.

```text
InventoryPro/
├── src/
│   ├── InventoryPro.Domain/          entities
│   ├── InventoryPro.Application/     DTOs, services, validators, insights engine
│   ├── InventoryPro.Infrastructure/  EF Core, migrations, repositories
│   └── InventoryPro.API/             controllers, auth, middleware, Swagger
├── tests/InventoryPro.Tests/         xUnit tests (SQLite in-memory)
├── frontend/                         React + TypeScript dashboard
└── .github/workflows/ci.yml
```

## Getting started

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), [Node.js 22+](https://nodejs.org), Git. No database server is needed for the SQLite development setup.

### 1. Backend

```bash
git clone https://github.com/kushal-pandey/InventoryPro.git
cd InventoryPro

dotnet restore
dotnet build InventoryPro.sln

# one-time: JWT signing key (kept out of source control)
dotnet user-secrets init --project src/InventoryPro.API
dotnet user-secrets set "Jwt:Key" "REPLACE_WITH_A_LONG_RANDOM_SECRET_OF_32_OR_MORE_CHARS" --project src/InventoryPro.API
```

The first administrator is created from **environment variables** (not user-secrets) so no default password ever ships in the repo. Set them in the same terminal before starting the API:

```bash
# macOS / Linux / Git Bash
export INVENTORYPRO_ADMIN_EMAIL="admin@inventorypro.local"
export INVENTORYPRO_ADMIN_PASSWORD="ChooseAStrongPassword1!"

# Windows PowerShell
$env:INVENTORYPRO_ADMIN_EMAIL="admin@inventorypro.local"
$env:INVENTORYPRO_ADMIN_PASSWORD="ChooseAStrongPassword1!"
```

```bash
dotnet run --project src/InventoryPro.API
```

Swagger: `http://localhost:5080/swagger`. Health: `/health/live` and `/health/ready` (checks the database).

### 2. Frontend

```bash
cd frontend
npm ci
cp .env.example .env.local     # points the UI at http://localhost:5080/api
npm run dev                    # http://localhost:5173
```

Create a Staff account from the sign-in page for local testing, or sign in with the Admin account you configured. To try the planner with realistic data, create a few products, add a customer, place orders, and open **Reorder planner**.

### Creating staff accounts

Public sign-up is disabled by default and in production. For local development, `appsettings.Development.json` enables it so the login page offers account creation; new accounts are Staff and can read customer and order data. Keep `Auth:AllowPublicRegistration` false in production. As an Admin, account creation remains available through `POST /api/auth/register` (Swagger → Authorize first).

## Tests

```bash
dotnet test InventoryPro.sln --configuration Release     # backend unit + API integration tests
cd frontend && npm run lint && npm test && npm run build # frontend
```

Highlights: API integration coverage for authentication, role authorization, validation, order rollback and concurrency, insights, rate limiting, CORS and pagination; forecasting rules (every stock-health state, floating-point edge cases), SQLite query regressions and frontend behavior. For a live API smoke check, set the admin environment variables and run `bash scripts/smoke.sh`. All HTTP endpoints and example payloads are in [api.http](./api.http).

### Browser end-to-end tests

```bash
cd frontend
npx playwright install chromium
npm run test:e2e
```

The Playwright configuration starts the API with a fresh temporary SQLite database and test-only Admin credentials, then starts Vite against that API. The browser journey creates a category, product, customer and order; verifies planner results and stock restoration on cancellation; adds a Staff member; and checks the Staff role cannot access Team or admin-only actions. It also checks a 390x844 viewport for horizontal overflow. The run writes desktop, mobile and dark-theme screenshots to `docs/screenshots/`.

| Page | Desktop | Mobile (390x844) | Dark theme |
| --- | --- | --- | --- |
| Dashboard | [Screenshot](./docs/screenshots/dashboard-desktop.png) | [Screenshot](./docs/screenshots/dashboard-mobile.png) | [Screenshot](./docs/screenshots/dashboard-dark.png) |
| Reorder planner | [Screenshot](./docs/screenshots/planner-desktop.png) | [Screenshot](./docs/screenshots/planner-mobile.png) | [Screenshot](./docs/screenshots/planner-dark.png) |
| Products | [Screenshot](./docs/screenshots/products-desktop.png) | [Screenshot](./docs/screenshots/products-mobile.png) | [Screenshot](./docs/screenshots/products-dark.png) |
| Orders | [Screenshot](./docs/screenshots/orders-desktop.png) | [Screenshot](./docs/screenshots/orders-mobile.png) | [Screenshot](./docs/screenshots/orders-dark.png) |

### Lighthouse

Measured after `npm run build` using `npm run preview` and a desktop Lighthouse audit of the unauthenticated app (which redirects to `/login`):

| Performance | Accessibility | Best Practices |
| ---: | ---: | ---: |
| 100 | 98 | 96 |

All Phase 5 targets were met: Performance >= 90, Accessibility >= 95, and Best Practices >= 95.

## Configuration

| Setting | Purpose |
| --- | --- |
| `Jwt:Key` (user-secrets / `Jwt__Key`) | JWT signing key, at least 32 characters; required |
| `INVENTORYPRO_ADMIN_EMAIL` / `INVENTORYPRO_ADMIN_PASSWORD` (environment variables) | creates the first Admin on startup; nothing is seeded if unset |
| `Cors:AllowedOrigins` | allowed frontend origins; required |
| `Insights:*` | planner defaults (lookback, lead time, safety, coverage, dead-stock, overstock days) |
| `Auth:AllowPublicRegistration` | `false` by default and in production; enabled in `Development` only |
| `DatabaseProvider` / `ConnectionStrings:*` | `Sqlite` (dev) or `SqlServer`. SQL Server needs its own generated migrations |
| `VITE_API_BASE_URL` (frontend) | API base URL including `/api` |

## Known limitations and roadmap

- The JWT is stored in `localStorage` (readable by any script on the page). For production, issue it in an `httpOnly` SameSite cookie.
- No refresh tokens, password reset or email verification.
- Insights aggregate in memory on a bounded result set, which is fine at small-business scale; very large catalogs would move this to SQL views or a background snapshot.
- Reorder suggestions assume one lead time for all products; per-supplier lead times and cost price are the next features.
- Roadmap: per-product lead times and suppliers, purchase-order records, expiry/batch tracking (FEFO) for perishables, and deployment with a hosted demo.

## Author

**Kushal Pandey** · [github.com/kushal-pandey](https://github.com/kushal-pandey)
