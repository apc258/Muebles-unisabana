using Testcontainers.PostgreSql;
using Xunit;

namespace InventoryService.Tests;

// Fixture compartido entre tests: arranca UN solo contenedor Postgres efímero
// para toda la suite y lo destruye al final. Equivalente al H2 in-memory del Word,
// pero contra el motor real (Postgres) porque InventoryService usa Npgsql.
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("inventory_test_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
