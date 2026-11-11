using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace InventoryPro.IntegrationTests;

internal sealed class ApiTestHost : IDisposable
{
    public const string AdminEmail = "admin@integration.test";
    public const string AdminPassword = "InventoryAdmin!2026";
    public const string CorsOrigin = "https://inventorypro.integration.test";
    private const string JwtKey = "InventoryPro.IntegrationTests.Jwt.Signing.Key.2026!Safe";

    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), $"inventorypro-integration-{Guid.NewGuid():N}.db");
    private readonly string? _previousAdminEmail;
    private readonly string? _previousAdminPassword;
    private readonly string? _previousJwtKey;
    private readonly string? _previousJwtIssuer;
    private readonly string? _previousJwtAudience;
    private readonly string? _previousCorsOrigin;
    private readonly string? _previousBenchmarkSeed;
    private readonly TestWebApplicationFactory _factory;

    public ApiTestHost(bool allowPublicRegistration = false)
    {
        _previousAdminEmail = Environment.GetEnvironmentVariable("INVENTORYPRO_ADMIN_EMAIL");
        _previousAdminPassword = Environment.GetEnvironmentVariable("INVENTORYPRO_ADMIN_PASSWORD");
        _previousJwtKey = Environment.GetEnvironmentVariable("Jwt__Key");
        _previousJwtIssuer = Environment.GetEnvironmentVariable("Jwt__Issuer");
        _previousJwtAudience = Environment.GetEnvironmentVariable("Jwt__Audience");
        _previousCorsOrigin = Environment.GetEnvironmentVariable("Cors__AllowedOrigins__0");
        _previousBenchmarkSeed = Environment.GetEnvironmentVariable("INVENTORYPRO_BENCHMARK_SEED");
        Environment.SetEnvironmentVariable("INVENTORYPRO_ADMIN_EMAIL", AdminEmail);
        Environment.SetEnvironmentVariable("INVENTORYPRO_ADMIN_PASSWORD", AdminPassword);
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "InventoryPro.API.IntegrationTests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "InventoryPro.Client.IntegrationTests");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", CorsOrigin);
        Environment.SetEnvironmentVariable("INVENTORYPRO_BENCHMARK_SEED", null);
        _factory = new TestWebApplicationFactory(_databasePath, allowPublicRegistration);
    }

    public HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("http://localhost")
    });

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable("INVENTORYPRO_ADMIN_EMAIL", _previousAdminEmail);
        Environment.SetEnvironmentVariable("INVENTORYPRO_ADMIN_PASSWORD", _previousAdminPassword);
        Environment.SetEnvironmentVariable("Jwt__Key", _previousJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", _previousJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", _previousJwtAudience);
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", _previousCorsOrigin);
        Environment.SetEnvironmentVariable("INVENTORYPRO_BENCHMARK_SEED", _previousBenchmarkSeed);

        foreach (var path in new[] { _databasePath, _databasePath + "-shm", _databasePath + "-wal" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath;
        private readonly bool _allowPublicRegistration;

        public TestWebApplicationFactory(string databasePath, bool allowPublicRegistration)
        {
            _databasePath = databasePath;
            _allowPublicRegistration = allowPublicRegistration;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "Sqlite",
                    ["ConnectionStrings:Sqlite"] = $"Data Source={_databasePath};Default Timeout=30;Pooling=False",
                    ["Jwt:Key"] = JwtKey,
                    ["Jwt:Issuer"] = "InventoryPro.API.IntegrationTests",
                    ["Jwt:Audience"] = "InventoryPro.Client.IntegrationTests",
                    ["Jwt:ExpiryMinutes"] = "60",
                    ["Auth:AllowPublicRegistration"] = _allowPublicRegistration.ToString(),
                    ["Cors:AllowedOrigins:0"] = CorsOrigin
                }));
        }
    }
}
