using System.Data;
using System.Runtime.CompilerServices;
using Npgsql;

[assembly: InternalsVisibleTo("CartService.Tests")]

var builder = WebApplication.CreateBuilder(args);
// --- AGREGA ESTO AQUÍ ---
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});
// ------------------------
var connectionString = builder.Configuration.GetConnectionString("CartDb")
    ?? builder.Configuration["DATABASE_URL"]
    ?? "Host=proyecto-muebles-postgres;Port=5432;Database=muebles_db;Username=postgres;Password=postgres";

builder.Services.AddSingleton(new CartDb(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CartDb>();
    db.Initialize();
}

app.MapGet("/health", () => Results.Ok(new { service = "CartService", database = "PostgreSQL" }));

// --- INICIO DE LA MODIFICACIÓN (Mantenemos todo tu código anterior) ---
app.MapGet("/api/cart/{customerId:guid}", (HttpContext httpContext, Guid customerId, CartDb db) =>
{
    var currentUserId = GetCurrentUserId(httpContext);
    
    // MODIFICACIÓN: Si no es admin y no es el dueño (o es invitado),
    // en lugar de 403, devolvemos un carrito vacío para no bloquear la carga del frontend.
    if (!IsAdmin(httpContext) && (currentUserId is null || currentUserId.Value != customerId))
    {
        return Results.Ok(new CartResponse(Guid.Empty, customerId, new List<CartItemResponse>(), 0));
    }

    var cart = db.GetOrCreateCart(customerId);
    return Results.Ok(cart);
});
// --- FIN DE LA MODIFICACIÓN ---

app.MapPost("/api/cart/items", (HttpContext httpContext, AddCartItemRequest request, CartDb db) =>
{
    // 1. Convertimos el string a GUID manualmente aquí dentro
    // Si el ID que envías desde el frontend NO es un UUID válido, Guid.Parse lanzará error.
    // Para depurar, si esto falla, sabremos que el ID del frontend está mal formado.
    
    Guid customerId;
    Guid productId;

    try {
        customerId = Guid.Parse(request.CustomerId);
        productId = Guid.Parse(request.ProductId);
    } catch {
        // Esto captura el error si el ID no es un UUID correcto
        return Results.BadRequest(new { 
            message = "Error: El ID enviado desde el frontend no es un UUID válido.",
            receivedCustomerId = request.CustomerId,
            receivedProductId = request.ProductId
        });
    }

    // 2. Validación de negocio
    if (request.Quantity <= 0 || request.UnitPrice < 0)
    {
        return Results.BadRequest(new { message = "Cantidad o precio inválidos." });
    }

    // 3. Ejecución
    var cart = db.GetOrCreateCart(customerId);
    db.AddItem(cart.Id, productId, request.Quantity, request.UnitPrice, request.ProductName ?? string.Empty);
    
    return Results.Ok(db.GetCartByCustomerId(customerId));
});

app.MapDelete("/api/cart/{customerId:guid}/items/{productId:guid}", (HttpContext httpContext, Guid customerId, Guid productId, CartDb db) =>
{
    var authorization = RequireOwnerOrAdmin(httpContext, customerId);
    if (authorization is not null)
    {
        return authorization;
    }

    db.RemoveItem(customerId, productId);
    return Results.Ok(new { message = "Item eliminado del carrito" });
});

app.MapDelete("/api/cart/{customerId:guid}/items", (HttpContext httpContext, Guid customerId, CartDb db) =>
{
    var authorization = RequireOwnerOrAdmin(httpContext, customerId);
    if (authorization is not null)
    {
        return authorization;
    }

    db.ClearCart(customerId);
    return Results.Ok(db.GetOrCreateCart(customerId));
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

static IResult? RequireOwnerOrAdmin(HttpContext httpContext, Guid ownerId)
{
    if (IsAdmin(httpContext))
    {
        return null;
    }

    var currentUserId = GetCurrentUserId(httpContext);
    if (currentUserId is null || currentUserId.Value != ownerId)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    return null;
}

public partial class Program { }

record AddCartItemRequest(string CustomerId, string ProductId, int Quantity, decimal UnitPrice, string? ProductName);
record CartResponse(Guid Id, Guid CustomerId, List<CartItemResponse> Items, decimal TotalAmount);
record CartItemResponse(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal);
record CartRecord(Guid Id, Guid CustomerId);

sealed class CartDb
{
    private readonly string _connectionString;

    public CartDb(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Initialize()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS carts (
                id UUID PRIMARY KEY,
                customer_id UUID NOT NULL UNIQUE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS cart_items (
                id UUID PRIMARY KEY,
                cart_id UUID NOT NULL REFERENCES carts(id) ON DELETE CASCADE,
                product_id UUID NOT NULL,
                product_name TEXT NOT NULL,
                quantity INTEGER NOT NULL,
                unit_price NUMERIC(18,2) NOT NULL,
                UNIQUE(cart_id, product_id)
            );
        ";
        command.ExecuteNonQuery();
    }

    public CartResponse GetOrCreateCart(Guid customerId)
    {
        var existing = GetCartByCustomerId(customerId);
        if (existing is not null)
        {
            return existing;
        }

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var cartId = Guid.NewGuid();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO carts (id, customer_id, created_at, updated_at)
            VALUES (@id, @customerId, NOW(), NOW());
        ";
        command.Parameters.AddWithValue("id", cartId);
        command.Parameters.AddWithValue("customerId", customerId);
        command.ExecuteNonQuery();

        return new CartResponse(cartId, customerId, new List<CartItemResponse>(), 0);
    }

    public void AddItem(Guid cartId, Guid productId, int quantity, decimal unitPrice, string productName)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO cart_items (id, cart_id, product_id, product_name, quantity, unit_price)
            VALUES (@id, @cartId, @productId, @productName, @quantity, @unitPrice)
            ON CONFLICT (cart_id, product_id)
            DO UPDATE SET
                quantity = cart_items.quantity + EXCLUDED.quantity,
                unit_price = EXCLUDED.unit_price,
                product_name = EXCLUDED.product_name;

            UPDATE carts
            SET updated_at = NOW()
            WHERE id = @cartId;
        ";
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("cartId", cartId);
        command.Parameters.AddWithValue("productId", productId);
        command.Parameters.AddWithValue("productName", productName);
        command.Parameters.AddWithValue("quantity", quantity);
        command.Parameters.AddWithValue("unitPrice", unitPrice);
        command.ExecuteNonQuery();
    }

    public void RemoveItem(Guid customerId, Guid productId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM cart_items
            WHERE cart_id = (SELECT id FROM carts WHERE customer_id = @customerId)
              AND product_id = @productId;

            UPDATE carts
            SET updated_at = NOW()
            WHERE customer_id = @customerId;
        ";
        command.Parameters.AddWithValue("customerId", customerId);
        command.Parameters.AddWithValue("productId", productId);
        command.ExecuteNonQuery();
    }

    public void ClearCart(Guid customerId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM cart_items
            WHERE cart_id = (SELECT id FROM carts WHERE customer_id = @customerId);

            UPDATE carts
            SET updated_at = NOW()
            WHERE customer_id = @customerId;
        ";
        command.Parameters.AddWithValue("customerId", customerId);
        command.ExecuteNonQuery();
    }

    public CartResponse? GetCartByCustomerId(Guid customerId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        Guid cartId;
        using (var cartCommand = connection.CreateCommand())
        {
            cartCommand.CommandText = @"
                SELECT id, customer_id
                FROM carts
                WHERE customer_id = @customerId
                LIMIT 1;
            ";
            cartCommand.Parameters.AddWithValue("customerId", customerId);

            using var reader = cartCommand.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            cartId = reader.GetGuid(0);
        }

        var items = new List<CartItemResponse>();
        using (var itemsCommand = connection.CreateCommand())
        {
            itemsCommand.CommandText = @"
                SELECT product_id, product_name, quantity, unit_price
                FROM cart_items
                WHERE cart_id = @cartId;
            ";
            itemsCommand.Parameters.AddWithValue("cartId", cartId);

            using var itemsReader = itemsCommand.ExecuteReader();
            while (itemsReader.Read())
            {
                var productId = itemsReader.GetGuid(0);
                var productName = itemsReader.GetString(1);
                var quantity = itemsReader.GetInt32(2);
                var unitPrice = itemsReader.GetDecimal(3);
                items.Add(new CartItemResponse(productId, productName, quantity, unitPrice, quantity * unitPrice));
            }
        }

        var subtotal = items.Sum(item => item.Subtotal);
        var tax = Math.Round(subtotal * 0.16m, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + tax;
        return new CartResponse(cartId, customerId, items, total);
    }
}
