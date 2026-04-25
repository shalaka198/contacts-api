using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Testcontainers.PostgreSql;
using Contacts.Infrastructure.Persistence;
using Xunit;

namespace Contacts.IntegrationTests.Fixtures;

/// <summary>
/// Spins up a real PostgreSQL container via Docker and replaces
/// the production DB connection for the duration of the test run.
/// The container is shared across all tests in the collection for speed.
/// </summary>
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("contacts_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the production DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            // Register with the Testcontainer's connection string
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Apply migrations against the test container
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        });
    }

    /// <summary>
    /// Creates an HTTP client pre-configured with a valid JWT from the demo auth endpoint.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "admin@example.com",
        string password = "Admin1234!")
    {
        var client = CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/token",
            new { email, password });

        loginResponse.EnsureSuccessStatusCode();

        var tokenBody = await loginResponse.Content.ReadFromJsonAsync<TokenBody>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenBody!.AccessToken);

        return client;
    }

    private sealed record TokenBody(string AccessToken, string TokenType, int ExpiresIn);
}

[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<ApiWebApplicationFactory> { }
