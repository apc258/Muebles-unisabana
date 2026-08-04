using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PaymentService.Tests;

public sealed class PaymentEndpointsSystemTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PaymentApiFactory _factory;

    public PaymentEndpointsSystemTests(PostgresContainerFixture fixture)
    {
        _factory = new PaymentApiFactory(fixture.ConnectionString);
    }

    // --- PRUEBA HTTP 1: POST /api/payments/authorize sin items retorna 400 ---
    [Fact(DisplayName = "Sistema: POST /api/payments/authorize sin items retorna 400")]
    public async Task Authorize_WithoutItems_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new
        {
            orderId = Guid.NewGuid(),
            customerId = "cliente-001",
            customerName = "Cliente",
            customerEmail = "cliente@muebles.com",
            paymentMethod = "Tarjeta",
            items = Array.Empty<object>()
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments/authorize", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- PRUEBA HTTP 2: POST authorize -> GET invoice/pdf devuelve application/pdf ---
    [Fact(DisplayName = "Sistema: GET /api/payments/{id}/invoice/pdf retorna 200 con Content-Type application/pdf")]
    public async Task GetInvoicePdf_ReturnsPdfFile()
    {
        // Arrange: primero creamos un payment real
        var client = _factory.CreateClient();
        var authorizePayload = new
        {
            orderId = Guid.NewGuid(),
            customerId = "cliente-001",
            customerName = "Cliente Demo",
            customerEmail = "cliente@muebles.com",
            paymentMethod = "Tarjeta",
            items = new[]
            {
                new { productId = Guid.NewGuid(), productName = "Sofá Oslo", quantity = 1, unitPrice = 2499m }
            }
        };

        var authResp = await client.PostAsJsonAsync("/api/payments/authorize", authorizePayload);
        Assert.Equal(HttpStatusCode.OK, authResp.StatusCode);
        var authBody = await authResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(authBody);
        var paymentId = doc.RootElement.GetProperty("paymentId").GetGuid();

        // Act
        var pdfResp = await client.GetAsync($"/api/payments/{paymentId}/invoice/pdf");

        // Assert
        Assert.Equal(HttpStatusCode.OK, pdfResp.StatusCode);
        Assert.Equal("application/pdf", pdfResp.Content.Headers.ContentType?.MediaType);

        var bytes = await pdfResp.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100, "PDF deberia tener contenido");
        // Cabecera PDF: %PDF
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }
}

internal sealed class PaymentApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public PaymentApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:PaymentDb", _connectionString);
        builder.UseSetting("DATABASE_URL", _connectionString);
        builder.UseEnvironment("Testing");
    }
}
