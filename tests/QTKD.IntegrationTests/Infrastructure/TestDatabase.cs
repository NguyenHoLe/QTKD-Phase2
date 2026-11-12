using Microsoft.Extensions.Configuration;
using Npgsql;

namespace QTKD.IntegrationTests.Infrastructure;

/// <summary>
/// Resolves the connection string of the dedicated test database.
/// The integration tests DROP and recreate this database on every run, so it must never
/// point at the development database <c>qtkd</c> or any database with real data.
/// </summary>
public static class TestDatabase
{
    public const string ConnectionStringName = "QtkdTest";

    // Same value as <UserSecretsId> in QTKD.IntegrationTests.csproj.
    private const string UserSecretsId = "badfb4de-a4f4-4a2a-b0f5-9f97fbba8471";

    public static string GetConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(UserSecretsId)
            .AddEnvironmentVariables() // ConnectionStrings__QtkdTest overrides user secrets (CI)
            .Build();

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Run: " +
                "dotnet user-secrets set \"ConnectionStrings:QtkdTest\" " +
                "\"Host=localhost;Port=5432;Database=qtkd_test;Username=postgres;Password=...\" " +
                "--project tests/QTKD.IntegrationTests");
        }

        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database;
        if (string.IsNullOrWhiteSpace(databaseName) ||
            !databaseName.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to run integration tests against database '{databaseName}'. " +
                "The tests drop and recreate their database, so its name must contain 'test' (for example qtkd_test).");
        }

        return connectionString;
    }
}
