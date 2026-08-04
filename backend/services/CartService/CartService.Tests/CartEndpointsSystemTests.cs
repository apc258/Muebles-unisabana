using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CartService.Tests;

public sealed class CartEndpointsSystemTests : IClassFixture<PostgresContainerFixture>
{
    private readonly CartApiFactory _factory;
    private static readonly Guid CustomerForTests = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public CartEndpointsSystemTests(PostgresContainerFixture fixture)
    {
        _factory = new CartApiFactory(fixture.ConnectionString);
    }

    // --- PRUEBA HTTP 1: POST /api/cart/items con Quantity = 0 retorna 400 ---
    [Fact(DisplayName = "Sistema: POST /api/cart/items con quantity=0 retorna 400")]
    public async Task AddItem_WithZeroQuantity_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new
        {
            customerId = CustomerForTests.ToString(),
            productId = Guid.NewGuid().ToString(),
            quantity = 0,
            unitPrice = 100m,
            productName = "Producto invalido"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/cart/items", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- PRUEBA HTTP 2: GET /api/cart/{customerId} con header Admin retorna 200 con cart real ---
    [Fact(DisplayName = "Sistema: GET /api/cart/{customerId} con header Admin retorna 200 con cart real")]
    public async Task GetCart_AsAdmin_ReturnsRealCart()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Role", "Admin");

        // Primero agregamos un item como ese usuario para que haya cart real
        await client.PostAsJsonAsync("/api/cart/items", new
        {
            customerId = CustomerForTests.ToString(),
            productId = Guid.NewGuid().ToString(),
            quantity = 2,
            unitPrice = 250m,
            productName = "Mesa"
        });

        // Act
        var response = await client.GetAsync($"/api/cart/{CustomerForTests}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(CustomerForTests, root.GetProperty("customerId").GetGuid());
        Assert.True(root.GetProperty("items").GetArrayLength() >= 1);
        // total = 500 (2*250) + IVA 16% (80) = 580
        Assert.Equal(580m, root.GetProperty("totalAmount").GetDecimal());
    }
}

internal sealed class CartApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CartApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:CartDb", _connectionString);
        builder.UseSetting("DATABASE_URL", _connectionString);
        builder.UseEnvironment("Testing");
    }
}
