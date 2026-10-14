# InventoryPro — Inventory & Order Management System

A full-stack inventory management application built with **ASP.NET Core 8, Entity Framework Core, and a layered architecture**. InventoryPro provides product catalog management, stock tracking, customer management, and order processing through a RESTful API secured with JWT authentication and role-based authorization.

The project focuses on practical backend engineering: maintainable architecture, validation, transactional order processing, consistent error handling, and automated testing.

## Features

- **Authentication & authorization** — JWT-based authentication with Admin and Staff roles.
- **Product management** — Create, update, search, filter, sort, and paginate products.
- **Inventory control** — Track stock levels, identify low-stock products, and record stock adjustments.
- **Order processing** — Create orders with stock validation and transactional inventory updates.
- **Customer management** — Maintain customer records and support order workflows.
- **Data integrity** — Entity relationships, transaction handling, and optimistic concurrency support.
- **Validation & error handling** — FluentValidation and centralized exception handling for consistent API responses.
- **Automated testing** — xUnit test suite covering application behavior.
- **API documentation** — Swagger UI for exploring and testing endpoints.

## Technology Stack

| Area              | Technologies                                               |
| ----------------- | ---------------------------------------------------------- |
| Backend           | C#, ASP.NET Core 8 Web API                                 |
| Architecture      | Domain, Application, Infrastructure, API                   |
| Database          | SQLite for development; SQL Server configuration available |
| ORM               | Entity Framework Core, LINQ                                |
| Authentication    | JWT, role-based authorization                              |
| Validation        | FluentValidation                                           |
| Object mapping    | AutoMapper                                                 |
| Data access       | Repository and Unit of Work patterns                       |
| API documentation | Swagger / OpenAPI                                          |
| Testing           | xUnit                                                      |
| Frontend          | React dashboard planned                                    |

## Architecture

InventoryPro uses a layered architecture that separates business rules, application workflows, infrastructure, and HTTP concerns.

```text
InventoryPro/
├── src/
│   ├── InventoryPro.Domain/
│   │   └── Entities and core domain models
│   ├── InventoryPro.Application/
│   │   └── DTOs, interfaces, services,
│   │       validators, mappings, common models
│   ├── InventoryPro.Infrastructure/
│   │   └── EF Core, DbContext, migrations,
│   │       repositories, Unit of Work
│   └── InventoryPro.API/
│       └── Controllers, authentication,
│           middleware, configuration, Swagger
├── tests/
│   └── InventoryPro.Tests/
├── InventoryPro.sln
└── README.md
```

### Key engineering decisions

- **Layered architecture:** Separates domain logic, application services, data access, and API concerns.
- **Repository and Unit of Work:** Organizes data access and coordinates persistence through EF Core.
- **Transactional order creation:** Protects order and stock updates from partial completion when an operation fails.
- **Optimistic concurrency:** Uses a product row-version field to help detect conflicting updates.
- **Soft deletion:** Preserves products with order history where applicable.
- **Database-side querying:** Uses composable LINQ for filtering, sorting, and pagination.
- **Centralized exception handling:** Converts application errors and validation failures into consistent HTTP responses.

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git
- No separate database server is required for the SQLite development setup.

### 1. Clone the repository

```bash
git clone https://github.com/kushal-pandey/InventoryPro.git
cd InventoryPro
```

### 2. Restore dependencies

```bash
dotnet restore
```

### 3. Build the solution

```bash
dotnet build InventoryPro.sln
```

### 4. Configure development secrets

The API requires a JWT signing key. Configure it through .NET User Secrets rather than committing a real signing key to source control.

```bash
dotnet user-secrets set "Jwt:Key" "YOUR_LONG_RANDOM_SECRET" --project src/InventoryPro.API
```

Initialize User Secrets for the project first if they have not already been initialized:

```bash
dotnet user-secrets init --project src/InventoryPro.API
```

Use the application's configured development database provider and connection string. The development configuration is intended to use SQLite.

### 5. Run the API

```bash
dotnet run --project src/InventoryPro.API
```

Open the Swagger URL printed in the terminal. For the current local setup, this may be:

- `http://localhost:5080/swagger`
- `https://localhost:5081/swagger`

The API applies configured EF Core migrations on startup. Database seeding depends on the application's configured initialization logic.

> **Development credentials:** If the development database is seeded with a default administrator, use the credentials configured by the seed implementation. Do not expose default credentials or development accounts in a public deployment.

## API Overview

| Area           | Example operations                                                     |
| -------------- | ---------------------------------------------------------------------- |
| Authentication | `POST /api/auth/register`, `POST /api/auth/login`                      |
| Products       | `GET /api/products`, `POST /api/products`, `PUT /api/products/{id}`    |
| Stock          | `GET /api/products/low-stock`, `PATCH /api/products/{id}/stock`        |
| Categories     | `GET /api/categories`, `POST /api/categories`                          |
| Customers      | `GET /api/customers`, `POST /api/customers`                            |
| Orders         | `GET /api/orders`, `POST /api/orders`, `PATCH /api/orders/{id}/status` |

Endpoints require authentication and/or specific roles according to their controller policies. Consult Swagger for the current request schemas, response formats, query parameters, and authorization requirements.

## Database Configuration

### SQLite development setup

SQLite provides a lightweight way to run the application locally without installing a database server. Use the development configuration and the migrations supported by that provider.

### SQL Server

A SQL Server deployment requires a valid SQL Server connection string, the appropriate provider configuration, and migrations compatible with SQL Server.

Do not assume SQLite migrations can be applied unchanged to SQL Server. Review and generate provider-specific migrations before deploying to a fresh SQL Server database.

Store connection strings and other credentials in User Secrets for local development or environment variables / a managed secret store in deployment environments.

## Running Tests

Run the complete test suite:

```bash
dotnet test InventoryPro.sln --configuration Release
```

Build the solution in Release mode:

```bash
dotnet build InventoryPro.sln --configuration Release
```

The latest reported verification completed successfully with **29 tests passed, 0 failed, and 0 skipped**. Run the commands again after making changes to confirm the current result.

## Security Notes

- Keep JWT signing keys and database credentials out of source control.
- Enforce authentication and role authorization on protected endpoints.
- Use HTTPS in deployed environments.
- Review CORS origins and environment-specific settings before deployment.
- Replace development credentials and settings before making the application publicly accessible.

## Roadmap

- [ ] Build a responsive React administration dashboard.
- [ ] Add inventory analytics and visual stock monitoring.
- [ ] Connect dashboard charts and tables to live API data.
- [ ] Add deployment instructions and a hosted demo.
- [ ] Include application screenshots and API examples.

## Author

**Kushal Pandey**

GitHub: [kushal-pandey](https://github.com/kushal-pandey)

---

*InventoryPro is a portfolio project demonstrating API development, layered architecture, data integrity, authentication, and automated testing with .NET.*
