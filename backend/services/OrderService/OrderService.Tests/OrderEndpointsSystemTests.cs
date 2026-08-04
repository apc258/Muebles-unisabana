using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrderService.Tests;

public sealed class OrderEndpointsSystemTests : IClassFixture<PostgresContainerFixture>
{
    private readonly OrderApiFactory _factory;

    public OrderEndpointsSystemTests(PostgresContainerFixture fixture)
    {
        _factory = new OrderApiFactory(fixture.ConnectionString);
    }

    // --- PRUEBA HTTP 1: POST sin header X-User-Id retorna 403 ---
    [Fact(DisplayName = "Sistema: POST /api/orders sin header X-User-Id retorna 403")]
    public async Task CreateOrder_WithoutUserHeader_ReturnsForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new
        {
            customerId = Guid.NewGuid(),
            items = new[] { new { productId = Guid.NewGuid(), quantity = 1, unitPrice = 100m } }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", payload);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- PRUEBA HTTP 2: PUT con header Admin y orden inexistente retorna 404 ---
    [Fact(DisplayName = "Sistema: PUT /api/orders/{id} como Admin con id inexistente retorna 404")]
    public async Task UpdateOrder_AsAdminButUnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Role", "Admin");
        var payload = new { status = "Paid" };

        // Act
        var response = await client.PutAsJsonAsync($"/api/orders/{Guid.NewGuid()}", payload);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

internal sealed class OrderApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public OrderApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:OrderDb", _connectionString);
        builder.UseSetting("DATABASE_URL", _connectionString);
        builder.UseEnvironment("Testing");
    }
}
