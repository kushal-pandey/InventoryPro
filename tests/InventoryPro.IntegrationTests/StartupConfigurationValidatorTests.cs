using InventoryPro.API;
using Microsoft.Extensions.Configuration;

namespace InventoryPro.IntegrationTests;

public sealed class StartupConfigurationValidatorTests
{
    [Fact]
    public void Development_configuration_allows_local_sqlite_and_public_registration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "Sqlite",
                ["ConnectionStrings:Sqlite"] = "Data Source=inventorypro-dev.db",
                ["Auth:AllowPublicRegistration"] = "true",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
            })
            .Build();

        var exception = Record.Exception(() =>
            StartupConfigurationValidator.ValidateProductionGuards(configuration, new TestHostEnvironment("Development")));

        Assert.Null(exception);
    }

    [Fact]
    public void Production_rejects_public_registration_when_enabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "Sqlite",
                ["ConnectionStrings:Sqlite"] = "Data Source=inventorypro.db",
                ["Auth:AllowPublicRegistration"] = "true",
                ["Cors:AllowedOrigins:0"] = "https://inventorypro.example.com"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            StartupConfigurationValidator.ValidateProductionGuards(configuration, new TestHostEnvironment("Production")));

        Assert.Contains("Auth:AllowPublicRegistration", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_rejects_localhost_cors_origins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "Sqlite",
                ["ConnectionStrings:Sqlite"] = "Data Source=inventorypro.db",
                ["Auth:AllowPublicRegistration"] = "false",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            StartupConfigurationValidator.ValidateProductionGuards(configuration, new TestHostEnvironment("Production")));

        Assert.Contains("Cors:AllowedOrigins", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_rejects_missing_database_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "Sqlite",
                ["Auth:AllowPublicRegistration"] = "false",
                ["Cors:AllowedOrigins:0"] = "https://inventorypro.example.com"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            StartupConfigurationValidator.ValidateProductionGuards(configuration, new TestHostEnvironment("Production")));

        Assert.Contains("ConnectionStrings:Sqlite", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "InventoryPro.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
