using InventoryPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InventoryPro.API;

/// <summary>
/// Creates an AppDbContext configured for PostgreSQL migration generation.
/// </summary>
public sealed class InventoryProDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Creates a PostgreSQL-configured context without connecting to a database.
    /// </summary>
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        const string connectionString =
            "Host=localhost;Port=5432;Database=inventorypro_design;" +
            "Username=design_time;Password=design_time";

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(
                "InventoryPro.PostgreSqlMigrations"));

        return new AppDbContext(optionsBuilder.Options);
    }
}