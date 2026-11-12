using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Data;

namespace QTKD.IntegrationTests.Infrastructure;

/// <summary>
/// Starts the real QTKD API in memory (same Program.cs, controllers, services and EF Core)
/// against the dedicated PostgreSQL test database.
/// The database is dropped and rebuilt from the migrations once per test run, so it always
/// contains exactly the seed data (2 customers, contract HD-2026-018, 1 price list).
/// </summary>
public sealed class QtkdApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string ConnectionString { get; } = TestDatabase.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" is not Development, so the API does not load the developer's user secrets
        // and cannot accidentally connect to the development database.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Qtkd", ConnectionString);

        // Keep the test output readable. EF Core logs every rejected write (unique key,
        // concurrency token) as an error even when the service handles it on purpose, which
        // the concurrency tests trigger many times. Unhandled exceptions are still logged by
        // the ASP.NET Core exception handler and show up as failing tests (HTTP 500).
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "None");
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore.HttpsPolicy", "Error");
    }

    /// <summary>
    /// Opens a separate DbContext straight on the test database so tests can check
    /// what was really stored, independently of the API responses.
    /// </summary>
    public QtkdDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<QtkdDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    public async Task InitializeAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}

/// <summary>
/// All integration test classes share one API instance and one database and run one class
/// at a time, so row counts checked by one test are not changed by another test class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<QtkdApiFactory>
{
    public const string Name = "QTKD API + PostgreSQL";
}
