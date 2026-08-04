using System.Runtime.CompilerServices;
using Npgsql;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

[assembly: InternalsVisibleTo("PaymentService.Tests")]

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("PaymentDb")
    ?? builder.Configuration["DATABASE_URL"]
    ?? "Host=proyecto-muebles-postgres;Port=5432;Database=muebles_db;Username=postgres;Password=postgres";

builder.Services.AddSingleton(new PaymentDb(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDb>();
    db.Initialize();
    db.Seed();
}

app.MapGet("/health", () => Results.Ok(new { service = "PaymentService", database = "PostgreSQL" }));

// --- AJUSTE EN RUTAS GET PARA EVITAR BLOQUEO DE FRONTEND ---
app.MapGet("/api/payments", (PaymentDb db) => 
{
    var payments = db.GetPayments();
    return Results.Ok(payments);
});

app.MapGet("/api/payments/{paymentId:guid}", (Guid paymentId, PaymentDb db) =>
{
    var payment = db.GetPayment(paymentId);
    // Si no encuentra el pago, devolvemos un NotFound para que el frontend lo maneje
    return payment is null ? Results.NotFound(new { message = "Pago no encontrado" }) : Results.Ok(payment);
});
// ------------------------------------------------------------

app.MapGet("/api/payments/{paymentId:guid}/invoice/pdf", (Guid paymentId, PaymentDb db) =>
{
    var invoice = db.GetInvoice(paymentId);
    if (invoice is null)
    {
        return Results.NotFound();
    }

    var pdf = InvoicePdfBuilder.Generate(invoice);
    return Results.File(pdf, "application/pdf", $"factura-{invoice.InvoiceNumber}.pdf");
});

app.MapPost("/api/payments/authorize", (AuthorizePaymentRequest request, PaymentDb db) =>
{
    if (request.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.CustomerEmail) || request.Items.Count == 0)
    {
        return Results.BadRequest(new { message = "orderId, customerName, customerEmail e items son obligatorios" });
    }

    var result = db.AuthorizePayment(request);
    return Results.Ok(result);
});

app.MapPut("/api/payments/{paymentId:guid}", (Guid paymentId, UpdatePaymentRequest request, PaymentDb db) =>
{
    var payment = db.UpdatePayment(paymentId, request);
    return payment is null ? Results.NotFound() : Results.Ok(payment);
});

app.MapDelete("/api/payments/{paymentId:guid}", (Guid paymentId, PaymentDb db) =>
{
    var deleted = db.DeletePayment(paymentId);
    return deleted ? Results.Ok(new { message = "Pago eliminado" }) : Results.NotFound();
});

app.Run();

public partial class Program { }

record AuthorizePaymentRequest(Guid OrderId, string CustomerId, string CustomerName, string CustomerEmail, string PaymentMethod, List<PaymentItemRequest> Items);
record PaymentItemRequest(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
record UpdatePaymentRequest(string Status, string PaymentMethod);
record PaymentResponse(Guid PaymentId, Guid OrderId, string CustomerId, string CustomerName, string CustomerEmail, string PaymentMethod, string Status, decimal Subtotal, decimal Tax, decimal Total, DateTime CreatedAt, InvoiceResponse Invoice);
record InvoiceResponse(Guid InvoiceId, Guid PaymentId, Guid OrderId, string CustomerId, string CustomerName, string CustomerEmail, string PaymentMethod, string InvoiceNumber, DateTime IssuedAt, decimal Subtotal, decimal Tax, decimal Total, List<InvoiceItemResponse> Items);
record InvoiceItemResponse(string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal);

sealed class PaymentDb
{
    private readonly string _connectionString;

    public PaymentDb(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Initialize()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS payments (
                payment_id UUID PRIMARY KEY,
                order_id UUID NOT NULL,
                customer_id TEXT NOT NULL,
                customer_name TEXT NOT NULL,
                customer_email TEXT NOT NULL,
                payment_method TEXT NOT NULL,
                status TEXT NOT NULL,
                subtotal NUMERIC(18,2) NOT NULL,
                tax NUMERIC(18,2) NOT NULL,
                total NUMERIC(18,2) NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS invoices (
                invoice_id UUID PRIMARY KEY,
                payment_id UUID NOT NULL REFERENCES payments(payment_id) ON DELETE CASCADE,
                invoice_number TEXT NOT NULL UNIQUE,
                issued_at TIMESTAMPTZ NOT NULL,
                subtotal NUMERIC(18,2) NOT NULL,
                tax NUMERIC(18,2) NOT NULL,
                total NUMERIC(18,2) NOT NULL
            );

            CREATE TABLE IF NOT EXISTS invoice_items (
                invoice_item_id UUID PRIMARY KEY,
                invoice_id UUID NOT NULL REFERENCES invoices(invoice_id) ON DELETE CASCADE,
                product_name TEXT NOT NULL,
                quantity INTEGER NOT NULL,
                unit_price NUMERIC(18,2) NOT NULL,
                subtotal NUMERIC(18,2) NOT NULL
            );
        ";
        command.ExecuteNonQuery();
    }

    public void Seed()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM payments;";
        var count = Convert.ToInt32(countCommand.ExecuteScalar());
        if (count > 0)
        {
            return;
        }

        AuthorizePayment(new AuthorizePaymentRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "cliente-demo-001",
            "Cliente Demo Uno",
            "cliente1@muebles.com",
            "Tarjeta",
            new List<PaymentItemRequest>
            {
                new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Sofá Nórdico Oslo", 1, 2499m),
                new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Mesa Comedor Luna", 1, 1899m)
            }));
    }

    public List<PaymentResponse> GetPayments()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT payment_id, order_id, customer_id, customer_name, customer_email, payment_method, status, subtotal, tax, total, created_at
            FROM payments
            ORDER BY created_at DESC;
        ";

        using var reader = command.ExecuteReader();
        var payments = new List<PaymentResponse>();
        while (reader.Read())
        {
            var paymentId = reader.GetGuid(0);
            payments.Add(new PaymentResponse(
                paymentId,
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.GetDateTime(10),
                GetInvoice(paymentId)!));
        }

        return payments;
    }

    public PaymentResponse? GetPayment(Guid paymentId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT payment_id, order_id, customer_id, customer_name, customer_email, payment_method, status, subtotal, tax, total, created_at
            FROM payments
            WHERE payment_id = @paymentId;
        ";
        command.Parameters.AddWithValue("paymentId", paymentId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PaymentResponse(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetDecimal(9),
            reader.GetDateTime(10),
            GetInvoice(paymentId)!);
    }

    public PaymentResponse AuthorizePayment(AuthorizePaymentRequest request)
    {
        var subtotal = request.Items.Sum(item => item.Quantity * item.UnitPrice);
        var tax = Math.Round(subtotal * 0.16m, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + tax;
        var paymentId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var issuedAt = DateTime.UtcNow;
        var invoiceNumber = $"FAC-{issuedAt:yyyyMMdd}-{paymentId.ToString()[..8].ToUpperInvariant()}";

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        using (var paymentCommand = connection.CreateCommand())
        {
            paymentCommand.Transaction = transaction;
            paymentCommand.CommandText = @"
                INSERT INTO payments (payment_id, order_id, customer_id, customer_name, customer_email, payment_method, status, subtotal, tax, total, created_at, updated_at)
                VALUES (@paymentId, @orderId, @customerId, @customerName, @customerEmail, @paymentMethod, @status, @subtotal, @tax, @total, NOW(), NOW());
            ";
            paymentCommand.Parameters.AddWithValue("paymentId", paymentId);
            paymentCommand.Parameters.AddWithValue("orderId", request.OrderId);
            paymentCommand.Parameters.AddWithValue("customerId", request.CustomerId.Trim());
            paymentCommand.Parameters.AddWithValue("customerName", request.CustomerName.Trim());
            paymentCommand.Parameters.AddWithValue("customerEmail", request.CustomerEmail.Trim());
            paymentCommand.Parameters.AddWithValue("paymentMethod", request.PaymentMethod.Trim());
            paymentCommand.Parameters.AddWithValue("status", "Authorized");
            paymentCommand.Parameters.AddWithValue("subtotal", subtotal);
            paymentCommand.Parameters.AddWithValue("tax", tax);
            paymentCommand.Parameters.AddWithValue("total", total);
            paymentCommand.ExecuteNonQuery();
        }

        using (var invoiceCommand = connection.CreateCommand())
        {
            invoiceCommand.Transaction = transaction;
            invoiceCommand.CommandText = @"
                INSERT INTO invoices (invoice_id, payment_id, invoice_number, issued_at, subtotal, tax, total)
                VALUES (@invoiceId, @paymentId, @invoiceNumber, @issuedAt, @subtotal, @tax, @total);
            ";
            invoiceCommand.Parameters.AddWithValue("invoiceId", invoiceId);
            invoiceCommand.Parameters.AddWithValue("paymentId", paymentId);
            invoiceCommand.Parameters.AddWithValue("invoiceNumber", invoiceNumber);
            invoiceCommand.Parameters.AddWithValue("issuedAt", issuedAt);
            invoiceCommand.Parameters.AddWithValue("subtotal", subtotal);
            invoiceCommand.Parameters.AddWithValue("tax", tax);
            invoiceCommand.Parameters.AddWithValue("total", total);
            invoiceCommand.ExecuteNonQuery();
        }

        foreach (var item in request.Items)
        {
            using var itemCommand = connection.CreateCommand();
            itemCommand.Transaction = transaction;
            itemCommand.CommandText = @"
                INSERT INTO invoice_items (invoice_item_id, invoice_id, product_name, quantity, unit_price, subtotal)
                VALUES (@id, @invoiceId, @productName, @quantity, @unitPrice, @subtotal);
            ";
            itemCommand.Parameters.AddWithValue("id", Guid.NewGuid());
            itemCommand.Parameters.AddWithValue("invoiceId", invoiceId);
            itemCommand.Parameters.AddWithValue("productName", item.ProductName.Trim());
            itemCommand.Parameters.AddWithValue("quantity", item.Quantity);
            itemCommand.Parameters.AddWithValue("unitPrice", item.UnitPrice);
            itemCommand.Parameters.AddWithValue("subtotal", item.Quantity * item.UnitPrice);
            itemCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return GetPayment(paymentId)!;
    }

    public PaymentResponse? UpdatePayment(Guid paymentId, UpdatePaymentRequest request)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE payments
            SET status = @status,
                payment_method = @paymentMethod,
                updated_at = NOW()
            WHERE payment_id = @paymentId;
        ";
        command.Parameters.AddWithValue("paymentId", paymentId);
        command.Parameters.AddWithValue("status", request.Status.Trim());
        command.Parameters.AddWithValue("paymentMethod", request.PaymentMethod.Trim());

        var rows = command.ExecuteNonQuery();
        return rows == 0 ? null : GetPayment(paymentId);
    }

    public bool DeletePayment(Guid paymentId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM payments WHERE payment_id = @paymentId;";
        command.Parameters.AddWithValue("paymentId", paymentId);
        return command.ExecuteNonQuery() > 0;
    }

    public InvoiceResponse? GetInvoice(Guid paymentId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.invoice_id, i.payment_id, p.order_id, p.customer_id, p.customer_name, p.customer_email, p.payment_method,
                   i.invoice_number, i.issued_at, i.subtotal, i.tax, i.total
            FROM invoices i
            INNER JOIN payments p ON p.payment_id = i.payment_id
            WHERE i.payment_id = @paymentId;
        ";
        command.Parameters.AddWithValue("paymentId", paymentId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var invoiceId = reader.GetGuid(0);
        return new InvoiceResponse(
            invoiceId,
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetDateTime(8),
            reader.GetDecimal(9),
            reader.GetDecimal(10),
            reader.GetDecimal(11),
            GetInvoiceItems(invoiceId));
    }

    private List<InvoiceItemResponse> GetInvoiceItems(Guid invoiceId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT product_name, quantity, unit_price, subtotal
            FROM invoice_items
            WHERE invoice_id = @invoiceId
            ORDER BY invoice_item_id;
        ";
        command.Parameters.AddWithValue("invoiceId", invoiceId);

        using var reader = command.ExecuteReader();
        var items = new List<InvoiceItemResponse>();
        while (reader.Read())
        {
            items.Add(new InvoiceItemResponse(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetDecimal(2),
                reader.GetDecimal(3)));
        }

        return items;
    }
}

static class InvoicePdfBuilder
{
    private static bool fontsRegistered;

    public static byte[] Generate(InvoiceResponse invoice)
    {
        EnsureFontsRegistered();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontFamily("Lato").FontSize(10).FontColor(Colors.Grey.Darken4));
                page.Header().Column(header =>
                {
                    header.Item().Text("Proyecto Muebles Modernos").FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                    header.Item().Text($"Factura {invoice.InvoiceNumber}").FontSize(14).FontColor(Colors.Grey.Darken2);
                    header.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });
                page.Content().Column(column =>
                {
                    column.Spacing(14);
                    column.Item().PaddingTop(16).Background(Colors.Grey.Lighten4).Padding(12).Column(details =>
                    {
                        details.Spacing(5);
                        details.Item().Text("Datos de la compra").FontSize(12).Bold().FontColor(Colors.Grey.Darken4);
                        details.Item().Text($"Fecha de emision: {invoice.IssuedAt:yyyy-MM-dd HH:mm:ss} UTC");
                        details.Item().Text($"Orden: {invoice.OrderId}");
                        details.Item().Text($"Pago: {invoice.PaymentId}");
                        details.Item().Text($"Cliente: {ValueOrDefault(invoice.CustomerName)}");
                        details.Item().Text($"Correo: {ValueOrDefault(invoice.CustomerEmail)}");
                        details.Item().Text($"Metodo de pago: {ValueOrDefault(invoice.PaymentMethod)}");
                    });
                    column.Item().Background(Colors.Blue.Lighten5).Padding(12).Row(row =>
                    {
                        row.RelativeItem().Column(totals =>
                        {
                            totals.Spacing(4);
                            totals.Item().Text($"Subtotal: ${invoice.Subtotal:N2}");
                            totals.Item().Text($"IVA 16%: ${invoice.Tax:N2}");
                        });
                        row.ConstantItem(160).AlignRight().Text($"Total: ${invoice.Total:N2}").FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                    });
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Producto").Bold().FontColor(Colors.White);
                            header.Cell().Element(HeaderCellStyle).Text("Cantidad").Bold().FontColor(Colors.White);
                            header.Cell().Element(HeaderCellStyle).Text("Precio").Bold().FontColor(Colors.White);
                            header.Cell().Element(HeaderCellStyle).Text("Subtotal").Bold().FontColor(Colors.White);
                        });

                        foreach (var item in invoice.Items.DefaultIfEmpty(new InvoiceItemResponse("Sin productos registrados", 0, 0, 0)))
                        {
                            table.Cell().Element(CellStyle).Text(ValueOrDefault(item.ProductName));
                            table.Cell().Element(CellStyle).Text(item.Quantity.ToString());
                            table.Cell().Element(CellStyle).Text($"${item.UnitPrice:N2}");
                            table.Cell().Element(CellStyle).Text($"${item.Subtotal:N2}");
                        }
                    });
                });
                page.Footer().AlignCenter().Text("Gracias por tu compra").FontSize(10).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();

        static string ValueOrDefault(string? value) => string.IsNullOrWhiteSpace(value) ? "No disponible" : value.Trim();
        static IContainer HeaderCellStyle(IContainer container) => container.Background(Colors.Blue.Darken3).PaddingVertical(6).PaddingHorizontal(5);
        static IContainer CellStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).PaddingHorizontal(5);
    }

    private static void EnsureFontsRegistered()
    {
        if (fontsRegistered)
        {
            return;
        }

        var fontDirectory = Path.Combine(AppContext.BaseDirectory, "runtimes", "any", "native", "LatoFont");
        foreach (var fileName in new[] { "Lato-Regular.ttf", "Lato-Bold.ttf" })
        {
            var fontPath = Path.Combine(fontDirectory, fileName);
            if (File.Exists(fontPath))
            {
                using var fontStream = File.OpenRead(fontPath);
                FontManager.RegisterFont(fontStream);
            }
        }

        fontsRegistered = true;
    }
}
