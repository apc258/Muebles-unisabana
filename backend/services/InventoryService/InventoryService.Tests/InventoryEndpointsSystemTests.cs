using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace InventoryService.Tests;

public sealed class InventoryEndpointsSystemTests : IClassFixture<PostgresContainerFixture>
{
    private readonly InventoryApiFactory _factory;

    public InventoryEndpointsSystemTests(PostgresContainerFixture fixture)
    {
        _factory = new InventoryApiFactory(fixture.ConnectionString);
    }

    // --- PRUEBA HTTP 1: POST con price=0 retorna 400 ---
    [Fact(DisplayName = "Sistema: POST /api/inventory/products con price=0 retorna 400")]
    public async Task CreateProduct_WithZeroPrice_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new
        {
            sku = "FAIL-001",
            name = "Mesa fallida",
            category = "comedores",
            price = 0m,
            image = "",
            colors = new[] { "Negro" },
            measures = new[] { "1m" },
            available = 1,
            reserved = 0,
            supplierName = "Tmp"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/inventory/products", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- PRUEBA HTTP 2: DELETE con guid inexistente retorna 404 ---
    [Fact(DisplayName = "Sistema: DELETE /api/inventory/products/{guid} inexistente retorna 404")]
    public async Task DeleteProduct_WithUnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync($"/api/inventory/products/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

internal sealed class InventoryApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public InventoryApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:InventoryDb", _connectionString);
        builder.UseSetting("DATABASE_URL", _connectionString);
        builder.UseEnvironment("Testing");
    }
}
