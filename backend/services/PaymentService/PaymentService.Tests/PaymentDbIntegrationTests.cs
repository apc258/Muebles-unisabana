using Npgsql;
using Xunit;

namespace PaymentService.Tests;

public sealed class PaymentDbIntegrationTests : IClassFixture<PostgresContainerFixture>, IDisposable
{
    private readonly PaymentDb _db;
    private readonly string _connectionString;
    private static readonly Guid OrderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public PaymentDbIntegrationTests(PostgresContainerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _db = new PaymentDb(_connectionString);
        _db.Initialize();
        TruncateTables();
    }

    public void Dispose() => TruncateTables();

    private void TruncateTables()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE invoice_items, invoices, payments RESTART IDENTITY CASCADE;";
        command.ExecuteNonQuery();
    }

    // --- PRUEBA 1: AuthorizePayment crea payment + invoice + items en una transaccion ---
    [Fact(DisplayName = "Integracion: AuthorizePayment crea payment, invoice e invoice_items en una transaccion")]
    public void AuthorizePayment_CreatesAllRecordsInTransaction()
    {
        // Arrange
        var request = new AuthorizePaymentRequest(
            OrderId,
            "cliente-001",
            "Cliente Demo",
            "cliente@muebles.com",
            "Tarjeta",
            new List<PaymentItemRequest>
            {
                new(Guid.NewGuid(), "Sofá Oslo", 1, 2499m),
                new(Guid.NewGuid(), "Mesa Luna", 1, 1899m)
            });

        // Act
        var result = _db.AuthorizePayment(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Authorized", result.Status);
        Assert.Equal(4398m, result.Subtotal);
        Assert.Equal(703.68m, result.Tax);
        Assert.Equal(5101.68m, result.Total);
        Assert.NotNull(result.Invoice);
        Assert.Equal(2, result.Invoice.Items.Count);
        Assert.StartsWith("FAC-", result.Invoice.InvoiceNumber);
    }

    // --- PRUEBA 2: GetInvoice retorna la factura con sus items (JOIN entre invoices/payments/invoice_items) ---
    [Fact(DisplayName = "Integracion: GetInvoice retorna factura con items asociados")]
    public void GetInvoice_ReturnsInvoiceWithItems()
    {
        // Arrange
        var created = _db.AuthorizePayment(new AuthorizePaymentRequest(
            OrderId, "c1", "Cliente", "c@x.com", "Tarjeta",
            new List<PaymentItemRequest> { new(Guid.NewGuid(), "Producto A", 2, 500m) }));

        // Act
        var invoice = _db.GetInvoice(created.PaymentId);

        // Assert
        Assert.NotNull(invoice);
        Assert.Equal(OrderId, invoice!.OrderId);
        Assert.Single(invoice.Items);
        Assert.Equal("Producto A", invoice.Items[0].ProductName);
        Assert.Equal(2, invoice.Items[0].Quantity);
        Assert.Equal(500m, invoice.Items[0].UnitPrice);
        Assert.Equal(1000m, invoice.Items[0].Subtotal);
    }

    // --- PRUEBA 3: UpdatePayment cambia status y payment_method ---
    [Fact(DisplayName = "Integracion: UpdatePayment cambia status y payment_method")]
    public void UpdatePayment_ChangesStatusAndMethod()
    {
        // Arrange
        var created = _db.AuthorizePayment(new AuthorizePaymentRequest(
            OrderId, "c2", "Cliente", "c@x.com", "Tarjeta",
            new List<PaymentItemRequest> { new(Guid.NewGuid(), "Prod", 1, 100m) }));

        // Act
        var updated = _db.UpdatePayment(created.PaymentId, new UpdatePaymentRequest("Refunded", "Efectivo"));

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("Refunded", updated!.Status);
        Assert.Equal("Efectivo", updated.PaymentMethod);
    }

    // --- PRUEBA 4: DeletePayment elimina con cascade invoice e invoice_items ---
    [Fact(DisplayName = "Integracion: DeletePayment elimina factura e items por cascade")]
    public void DeletePayment_CascadesToInvoiceAndItems()
    {
        // Arrange
        var created = _db.AuthorizePayment(new AuthorizePaymentRequest(
            OrderId, "c3", "Cliente", "c@x.com", "Tarjeta",
            new List<PaymentItemRequest> { new(Guid.NewGuid(), "Prod", 1, 100m) }));

        // Act
        var deleted = _db.DeletePayment(created.PaymentId);

        // Assert
        Assert.True(deleted);
        Assert.Null(_db.GetPayment(created.PaymentId));

        // Cascade: verificamos directamente que no hay invoices ni invoice_items huerfanos
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT (SELECT COUNT(*) FROM invoices) + (SELECT COUNT(*) FROM invoice_items);";
        var orphans = Convert.ToInt64(command.ExecuteScalar());
        Assert.Equal(0L, orphans);
    }

    // --- PRUEBA 5: DeletePayment con id desconocido retorna false ---
    [Fact(DisplayName = "Integracion: DeletePayment con id inexistente retorna false")]
    public void DeletePayment_WithUnknownId_ReturnsFalse()
    {
        var deleted = _db.DeletePayment(Guid.NewGuid());
        Assert.False(deleted);
    }
}
