using System.Data;
using System.Runtime.CompilerServices;
using Npgsql;

[assembly: InternalsVisibleTo("OrderService.Tests")]

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("OrderDb")
    ?? builder.Configuration["DATABASE_URL"]
    ?? "Host=proyecto-muebles-postgres;Port=5432;Database=muebles_db;Username=postgres;Password=postgres";

builder.Services.AddSingleton(new OrderDb(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDb>();
    db.Initialize();
    db.Seed();
}

app.MapGet("/health", () => Results.Ok(new { service = "OrderService", database = "PostgreSQL" }));

// --- MODIFICACIONES REALIZADAS ABAJO EN LOS MAPGET ---
app.MapGet("/api/orders", (HttpContext httpContext, OrderDb db) =>
{
    if (IsAdmin(httpContext))
    {
        return Results.Ok(db.GetOrders());
    }

    var currentUserId = GetCurrentUserId(httpContext);
    if (currentUserId is null)
    {
        // Se permite el acceso al invitado devolviendo lista vacía en lugar de 403
        return Results.Ok(new List<OrderResponse>());
    }

    return Results.Ok(db.GetOrdersByCustomerId(currentUserId.Value));
});

app.MapGet("/api/orders/{orderId:guid}", (HttpContext httpContext, Guid orderId, OrderDb db) =>
{
    var order = db.GetOrder(orderId);
    if (order is null)
    {
        return Results.NotFound(new { message = "Orden no encontrada" });
    }

    if (!IsAdmin(httpContext))
    {
        var currentUserId = GetCurrentUserId(httpContext);
        if (currentUserId is null || currentUserId.Value != order.CustomerId)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    return Results.Ok(order);
});
// -----------------------------------------------------

app.MapPost("/api/orders", (HttpContext httpContext, CreateOrderRequest request, OrderDb db) =>
{
    var currentUserId = GetCurrentUserId(httpContext);
    if (currentUserId is null)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (!IsAdmin(httpContext) && currentUserId.Value != request.CustomerId)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.CustomerId == Guid.Empty || request.Items is null || request.Items.Count == 0)
    {
        return Results.BadRequest(new { message = "customerId y al menos un item son obligatorios" });
    }

    if (request.Items.Any(item => item.ProductId == Guid.Empty || item.Quantity <= 0 || item.UnitPrice <= 0))
    {
        return Results.BadRequest(new { message = "Todos los items deben tener productId, quantity y unitPrice válidos" });
    }

    var order = db.CreateOrder(request);
    return Results.Created($"/api/orders/{order.OrderId}", order);
});

app.MapPut("/api/orders/{orderId:guid}", (HttpContext httpContext, Guid orderId, UpdateOrderRequest request, OrderDb db) =>
{
    var authorization = RequireAdmin(httpContext);
    if (authorization is not null)
    {
        return authorization;
    }

    var updated = db.UpdateOrder(orderId, request);
    return updated is null ? Results.NotFound(new { message = "Orden no encontrada" }) : Results.Ok(updated);
});

app.MapDelete("/api/orders/{orderId:guid}", (HttpContext httpContext, Guid orderId, OrderDb db) =>
{
    var authorization = RequireAdmin(httpContext);
    if (authorization is not null)
    {
        return authorization;
    }

    var deleted = db.DeleteOrder(orderId);
    return deleted ? Results.Ok(new { message = "Orden eliminada" }) : Results.NotFound(new { message = "Orden no encontrada" });
});

app.Run();

static Guid? GetCurrentUserId(HttpContext httpContext)
{
    var raw = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
    return Guid.TryParse(raw, out var userId) ? userId : null;
}

static string? GetCurrentUserRole(HttpContext httpContext)
{
    return httpContext.Request.Headers["X-User-Role"].FirstOrDefault();
}

static bool IsAdmin(HttpContext httpContext)
{
    return string.Equals(GetCurrentUserRole(httpContext), "Admin", StringComparison.OrdinalIgnoreCase);
}

static IResult? RequireAdmin(HttpContext httpContext)
{
    return IsAdmin(httpContext)
        ? null
        : Results.StatusCode(StatusCodes.Status403Forbidden);
}

public partial class Program { }

record CreateOrderRequest(Guid CustomerId, List<CreateOrderItemRequest> Items);
record CreateOrderItemRequest(Guid ProductId, int Quantity, decimal UnitPrice);
record UpdateOrderRequest(string Status);
record OrderResponse(Guid OrderId, Guid CustomerId, string Status, decimal Subtotal, decimal Tax, decimal Total, DateTime CreatedAt, DateTime UpdatedAt, List<OrderItemResponse> Items);
record OrderItemResponse(Guid OrderItemId, Guid ProductId, int Quantity, decimal UnitPrice, decimal Subtotal);

sealed class OrderDb
{
    private readonly string _connectionString;

    public OrderDb(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Initialize()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS orders (
                order_id UUID PRIMARY KEY,
                customer_id UUID NOT NULL,
                status TEXT NOT NULL,
                subtotal NUMERIC(18,2) NOT NULL,
                tax NUMERIC(18,2) NOT NULL,
                total NUMERIC(18,2) NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS order_items (
                order_item_id UUID PRIMARY KEY,
                order_id UUID NOT NULL REFERENCES orders(order_id) ON DELETE CASCADE,
                product_id UUID NOT NULL,
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
        countCommand.CommandText = "SELECT COUNT(*) FROM orders;";
        var count = Convert.ToInt32(countCommand.ExecuteScalar());
        if (count > 0)
        {
            return;
        }

        var order1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var order2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var customer1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var customer2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var product1 = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var product2 = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var product3 = Guid.Parse("10000000-0000-0000-0000-000000000003");

        using var transaction = connection.BeginTransaction();

        InsertOrder(connection, transaction, order1, customer1, "Created", 4398m, 703.68m, 5101.68m);
        InsertOrderItem(connection, transaction, Guid.NewGuid(), order1, product1, 1, 2499m, 2499m);
        InsertOrderItem(connection, transaction, Guid.NewGuid(), order1, product2, 1, 1899m, 1899m);

        InsertOrder(connection, transaction, order2, customer2, "Paid", 3199m, 511.84m, 3710.84m);
        InsertOrderItem(connection, transaction, Guid.NewGuid(), order2, product3, 1, 3199m, 3199m);

        transaction.Commit();
    }

    public List<OrderResponse> GetOrders()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT order_id, customer_id, status, subtotal, tax, total, created_at, updated_at
            FROM orders
            ORDER BY created_at DESC;
        ";

        var orders = new List<OrderResponse>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var orderId = reader.GetGuid(0);
            orders.Add(new OrderResponse(
                orderId,
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.GetDateTime(6),
                reader.GetDateTime(7),
                GetOrderItems(orderId)));
        }

        return orders;
    }

    public List<OrderResponse> GetOrdersByCustomerId(Guid customerId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT order_id, customer_id, status, subtotal, tax, total, created_at, updated_at
            FROM orders
            WHERE customer_id = @customerId
            ORDER BY created_at DESC;
        ";
        command.Parameters.AddWithValue("customerId", customerId);

        var orders = new List<OrderResponse>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var orderId = reader.GetGuid(0);
            orders.Add(new OrderResponse(
                orderId,
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.GetDateTime(6),
                reader.GetDateTime(7),
                GetOrderItems(orderId)));
        }

        return orders;
    }

    public OrderResponse? GetOrder(Guid orderId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT order_id, customer_id, status, subtotal, tax, total, created_at, updated_at
            FROM orders
            WHERE order_id = @orderId;
        ";
        command.Parameters.AddWithValue("orderId", orderId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new OrderResponse(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetDecimal(5),
            reader.GetDateTime(6),
            reader.GetDateTime(7),
            GetOrderItems(orderId));
    }

    public OrderResponse CreateOrder(CreateOrderRequest request)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var subtotal = request.Items.Sum(item => item.Quantity * item.UnitPrice);
        var tax = Math.Round(subtotal * 0.16m, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + tax;
        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        InsertOrder(connection, transaction, orderId, request.CustomerId, "Created", subtotal, tax, total, now);

        foreach (var item in request.Items)
        {
            InsertOrderItem(connection, transaction, Guid.NewGuid(), orderId, item.ProductId, item.Quantity, item.UnitPrice, item.Quantity * item.UnitPrice);
        }

        transaction.Commit();
        return GetOrder(orderId)!;
    }

    public OrderResponse? UpdateOrder(Guid orderId, UpdateOrderRequest request)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE orders
            SET status = @status, updated_at = NOW()
            WHERE order_id = @orderId;
        ";
        command.Parameters.AddWithValue("status", string.IsNullOrWhiteSpace(request.Status) ? "Created" : request.Status.Trim());
        command.Parameters.AddWithValue("orderId", orderId);

        var rows = command.ExecuteNonQuery();
        return rows == 0 ? null : GetOrder(orderId);
    }

    public bool DeleteOrder(Guid orderId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM orders WHERE order_id = @orderId;";
        command.Parameters.AddWithValue("orderId", orderId);
        return command.ExecuteNonQuery() > 0;
    }

    private List<OrderItemResponse> GetOrderItems(Guid orderId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT order_item_id, product_id, quantity, unit_price, subtotal
            FROM order_items
            WHERE order_id = @orderId
            ORDER BY order_item_id;
        ";
        command.Parameters.AddWithValue("orderId", orderId);

        var items = new List<OrderItemResponse>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new OrderItemResponse(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4)));
        }

        return items;
    }

    private static void InsertOrder(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid orderId, Guid customerId, string status, decimal subtotal, decimal tax, decimal total, DateTime? now = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO orders (order_id, customer_id, status, subtotal, tax, total, created_at, updated_at)
            VALUES (@orderId, @customerId, @status, @subtotal, @tax, @total, @createdAt, @updatedAt);
        ";
        var timestamp = now ?? DateTime.UtcNow;
        command.Parameters.AddWithValue("orderId", orderId);
        command.Parameters.AddWithValue("customerId", customerId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("subtotal", subtotal);
        command.Parameters.AddWithValue("tax", tax);
        command.Parameters.AddWithValue("total", total);
        command.Parameters.AddWithValue("createdAt", timestamp);
        command.Parameters.AddWithValue("updatedAt", timestamp);
        command.ExecuteNonQuery();
    }

    private static void InsertOrderItem(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid orderItemId, Guid orderId, Guid productId, int quantity, decimal unitPrice, decimal subtotal)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO order_items (order_item_id, order_id, product_id, quantity, unit_price, subtotal)
            VALUES (@orderItemId, @orderId, @productId, @quantity, @unitPrice, @subtotal);
        ";
        command.Parameters.AddWithValue("orderItemId", orderItemId);
        command.Parameters.AddWithValue("orderId", orderId);
        command.Parameters.AddWithValue("productId", productId);
        command.Parameters.AddWithValue("quantity", quantity);
        command.Parameters.AddWithValue("unitPrice", unitPrice);
        command.Parameters.AddWithValue("subtotal", subtotal);
        command.ExecuteNonQuery();
    }
}
