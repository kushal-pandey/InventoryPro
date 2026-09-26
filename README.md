# InventoryPro — Inventory & Order Management API

A layered ASP.NET Core Web API for managing a product catalog, stock levels, customers,
and orders, with JWT authentication and role-based authorization. Built to demonstrate
production-style backend patterns rather than a bare CRUD app.

## What it demonstrates

| Skill (from job description) | Where |
|---|---|
| C#, ASP.NET Core, .NET Core | Whole solution, .NET 8 |
| ASP.NET Core Web API | `InventoryPro.API/Controllers` |
| SQL Server | EF Core `UseSqlServer` (default provider) |
| EF Core + LINQ | `Infrastructure/Data`, filtering/sorting/paging in `ProductService` |
| OOP / clean coding practices | 4-layer architecture, SOLID-ish service/repository split |
| Debugging-friendly design | Centralized exception middleware, structured error responses |

Architecture (dependencies point inward, `API → Infrastructure/Application → Domain`):

```
InventoryPro.Domain          Entities only (Product, Order, Customer, Category, OrderItem). No dependencies.
InventoryPro.Application      DTOs, service interfaces + implementations, AutoMapper profiles,
                               FluentValidation validators, IUnitOfWork/IGenericRepository interfaces.
InventoryPro.Infrastructure    EF Core DbContext, migrations, repository + unit-of-work implementations.
InventoryPro.API               Controllers, JWT/Identity setup, Swagger, global exception middleware.
```

### Notable design decisions worth mentioning in an interview
- **Generic repository + Unit of Work** over `AppDbContext`, so a single `SaveChangesAsync()`
  commits everything in a request — used in `OrderService.CreateAsync` inside a DB
  transaction so a failed stock check rolls back the whole order, not just one line.
- **Optimistic concurrency** on `Product` via a `rowversion` column (`RowVersion` byte[]).
- **Soft delete** for products that already have order history, hard delete otherwise.
- **Pagination/filtering/sorting** built with composable LINQ (`ProductService.GetAllAsync`),
  not fetched-then-filtered-in-memory.
- **Global exception middleware** turns `NotFoundException` / `ConflictException` /
  FluentValidation errors into consistent JSON responses with the right status codes —
  controllers have no try/catch.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- One of:
  - **SQL Server** (LocalDB, Developer Edition, or Docker) — used by default in `appsettings.json`
  - **Nothing else** — the `Development` environment (the default when you run `dotnet run`)
    is pre-configured to use **SQLite** instead, so the API runs instantly with zero
    external dependencies. Switch back to SQL Server any time (see below).

## Getting it running (fastest path — SQLite, no SQL Server needed)

```bash
git clone <your-repo-url>
cd InventoryPro

# Restore all NuGet packages for every project in the solution
dotnet restore

# Build to verify everything compiles
dotnet build

# Run the API (uses appsettings.Development.json -> SQLite by default)
dotnet run --project src/InventoryPro.API
```

On first run the app automatically:
1. Applies EF Core migrations (creates `inventorypro.db` in the working directory)
2. Seeds two roles (`Admin`, `Staff`), a default admin user, two categories, and three products

Then open **https://localhost:5081/swagger** (or the `http://localhost:5080/swagger`
shown in the console) — Swagger launches automatically.

### Default login (seeded)
```
Email:    admin@inventorypro.local
Password: Admin@12345
```

Use `POST /api/auth/login` with these credentials, copy the returned `token`, click
**Authorize** in Swagger, and enter `Bearer <token>`.

## Running against real SQL Server instead

1. Update `ConnectionStrings:SqlServer` in `src/InventoryPro.API/appsettings.json`
   (or override via user-secrets / environment variable — see below).
2. Force the SQL Server provider by setting the environment variable:
   ```bash
   # macOS/Linux
   export DatabaseProvider=SqlServer
   # Windows (PowerShell)
   $env:DatabaseProvider="SqlServer"
   ```
3. Generate an initial migration (if you've changed the schema, or want a fresh one
   targeted at SQL Server) and run it:
   ```bash
   dotnet tool install --global dotnet-ef   # once, if you don't have it
   cd src/InventoryPro.API
   dotnet ef migrations add InitialCreate --project ../InventoryPro.Infrastructure --startup-project .
   dotnet ef database update --project ../InventoryPro.Infrastructure --startup-project .
   ```
4. `dotnet run --project src/InventoryPro.API`

> The app calls `context.Database.Migrate()` on startup, so once a migration exists,
> plain `dotnet run` will create/update the database automatically — step 3's
> `database update` is only needed if you want to apply it without starting the API.

## Configuration

All settings live in `src/InventoryPro.API/appsettings.json` (defaults) and
`appsettings.Development.json` (dev overrides). Don't commit real secrets — for anything
beyond local dev, use `dotnet user-secrets` or environment variables:

```bash
cd src/InventoryPro.API
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "some-long-random-signing-secret-min-32-chars"
dotnet user-secrets set "ConnectionStrings:SqlServer" "your-real-connection-string"
```

## API overview

| Endpoint | Auth | Notes |
|---|---|---|
| `POST /api/auth/register` | none | creates a `Staff` user |
| `POST /api/auth/login` | none | returns JWT |
| `GET /api/products?search=&categoryId=&minPrice=&maxPrice=&sortBy=&pageNumber=&pageSize=` | any authenticated user | paged + filtered |
| `GET /api/products/low-stock` | any authenticated user | stock ≤ reorder level |
| `POST /api/products` | Admin, Staff | |
| `PUT /api/products/{id}` | Admin, Staff | |
| `PATCH /api/products/{id}/stock` | Admin, Staff | manual stock adjustment with reason |
| `DELETE /api/products/{id}` | Admin | soft-deletes if it has order history |
| `GET/POST /api/categories`, `DELETE /api/categories/{id}` | mixed | |
| `GET/POST /api/customers` | any authenticated user | |
| `GET /api/orders`, `POST /api/orders`, `PATCH /api/orders/{id}/status` | Admin, Staff | order creation validates & decrements stock transactionally |

Full request/response schemas are in Swagger once the app is running.

## Project structure

```
InventoryPro.sln
src/
  InventoryPro.Domain/          Entities/ (Product, Category, Customer, Order, OrderItem, ApplicationUser)
  InventoryPro.Application/     DTOs/, Interfaces/, Services/, Mappings/, Validators/, Common/
  InventoryPro.Infrastructure/  Data/ (AppDbContext, seed), Repositories/ (generic repo + unit of work)
  InventoryPro.API/             Controllers/, Middleware/, Program.cs, appsettings*.json
.gitignore
README.md
```

## Verifying it builds before you push

```bash
dotnet restore
dotnet build --configuration Release
```

If `dotnet build` succeeds with 0 errors, you're safe to push.
