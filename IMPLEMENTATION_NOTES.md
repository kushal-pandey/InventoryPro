# InventoryPro hardening notes

This update applies the following changes:

- Added a global MVC filter that executes registered FluentValidation validators for controller DTOs.
- Aligned registration validation with the Identity password policy (12 characters plus uppercase, lowercase, digit, and symbol).
- Enabled Identity lockout checks on failed logins and IP-based rate limiting for authentication endpoints.
- Kept public registration restricted to the `Staff` role; product creation, updates, stock adjustment, and deletion now require `Admin`.
- Replaced random order-number suffixes with a high-entropy GUID-derived reference.
- Reworked order stock deduction to use conditional database-side updates within a transaction, including grouping duplicate product lines.
- Added guarded order status transitions and transactional, one-time stock restoration when cancelling a Pending or Confirmed order.
- Changed manual stock adjustments to conditional database-side updates and prevented stale product updates from overwriting stock deductions.
- Added pagination for orders, categories, and customers; page sizes are capped at 100.
- Added `/health/live` and `/health/ready`, correlation IDs, structured request logs, and stricter JWT/CORS startup checks.
- Made startup migrations Development-only by default; production migration is opt-in.
- Updated EF Core/JWT package alignment and pinned patched transitive versions for caching, Azure Identity/MSAL, and SQLite native runtime; verify the resolved graph with `dotnet list package --vulnerable --include-transitive`.\n- Updated README setup, security, role, pagination, and migration guidance.
- Added a cancellation/restocking regression test.

## Important limitations

- **Build and tests were not executed in this environment** because the .NET SDK/CLI is unavailable here. Run the verification commands below on the development machine before merging.
- The checked-in migration is SQLite-specific. A separate SQL Server migration set/assembly still needs to be created before using SQL Server with a newly provisioned database.
- A refresh-token flow is not implemented; the API continues to issue short-lived access tokens only.
- Built-in `ILogger` structured request logging is configured, but no external log aggregation, metrics, or distributed tracing backend is configured.
- Production CORS origins and JWT signing key must be supplied through secure configuration. Do not commit secrets.

## Verify locally

From the repository root:

```powershell
dotnet restore
dotnet build InventoryPro.sln --configuration Release
dotnet test tests/InventoryPro.Tests/InventoryPro.Tests.csproj --configuration Release
dotnet list package --vulnerable --include-transitive
git diff --check
```

Before running, configure a random JWT secret of at least 32 UTF-8 bytes using User Secrets or `Jwt__Key`. For an initial administrator account only, configure `INVENTORYPRO_ADMIN_EMAIL` and `INVENTORYPRO_ADMIN_PASSWORD`.
