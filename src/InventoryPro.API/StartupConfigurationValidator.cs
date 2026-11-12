using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace InventoryPro.API;

/// <summary>
/// Applies production-only startup constraints for public registration, CORS, and database configuration.
/// </summary>
public static class StartupConfigurationValidator
{
    /// <summary>
    /// Validates the configuration required to keep production settings safe and explicit.
    /// </summary>
    public static void ValidateProductionGuards(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return;

        if (configuration.GetValue<bool>("Auth:AllowPublicRegistration"))
        {
            throw new InvalidOperationException(
                "Production environment must disable Auth:AllowPublicRegistration.");
        }

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();

        if (allowedOrigins.Length == 0 ||
            allowedOrigins.Any(origin => string.IsNullOrWhiteSpace(origin) ||
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                !uri.IsAbsoluteUri ||
                IsLocalOrigin(uri)))
        {
            throw new InvalidOperationException(
                "Production environment requires explicit, non-local Cors:AllowedOrigins values.");
        }

        var provider = configuration["DatabaseProvider"];
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException(
                "DatabaseProvider must be explicitly configured in production.");
        }

        if (!provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) &&
            !provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) && !provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))

        {
            throw new InvalidOperationException(
                $"Unsupported DatabaseProvider '{provider}' in production. Use 'Sqlite', 'SqlServer' or 'PostgreSQL'.");
        }

        var connectionString = configuration.GetConnectionString(provider);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{provider} must be configured in production.");
        }
    }

    private static bool IsLocalOrigin(Uri uri)
    {
        var host = uri.Host;
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }
}
