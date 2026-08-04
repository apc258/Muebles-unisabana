using System.Data;
using System.Runtime.CompilerServices;
using Npgsql;

[assembly: InternalsVisibleTo("InventoryService.Tests")]

var builder = WebApplication.CreateBuilder(args);

// Corrección: Usamos 'proyecto-muebles-postgres' y 'muebles_db'
var connectionString = builder.Configuration.GetConnectionString("InventoryDb")
    ?? builder.Configuration["DATABASE_URL"]
    ?? "Host=proyecto-muebles-postgres;Port=5432;Database=muebles_db;Username=postgres;Password=postgres";

builder.Services.AddSingleton(new InventoryDb(connectionString));
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDb>();
    db.Initialize();
    db.Seed();
    db.UpdateSeedProductImages();
}

app.MapGet("/health", () => Results.Ok(new { service = "InventoryService", database = "PostgreSQL" }));

app.MapGet("/api/inventory/products", (InventoryDb db) => Results.Ok(db.GetProducts()));

app.MapGet("/api/inventory/products/{productId:guid}", (Guid productId, InventoryDb db) =>
{
    var product = db.GetProductById(productId);
    return product is null ? Results.NotFound(new { message = "Producto no encontrado" }) : Results.Ok(product);
});

app.MapGet("/api/inventory/{productId:guid}", (Guid productId, InventoryDb db) =>
{
    var product = db.GetProductById(productId);
    return product is null
        ? Results.NotFound(new { message = "Producto no encontrado" })
        : Results.Ok(new { productId = product.ProductId, available = product.Available, reserved = product.Reserved });
});

app.MapPost("/api/inventory/products", (CreateInventoryProductRequest request, InventoryDb db) =>
{
    if (string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Category) || request.Price <= 0)
    {
        return Results.BadRequest(new { message = "sku, name, category y price son obligatorios" });
    }

    var product = db.CreateProduct(request);
    return Results.Created($"/api/inventory/products/{product.ProductId}", product);
});

app.MapPut("/api/inventory/products/{productId:guid}", (Guid productId, UpdateInventoryProductRequest request, InventoryDb db) =>
{
    var product = db.UpdateProduct(productId, request);
    return product is null ? Results.NotFound(new { message = "Producto no encontrado" }) : Results.Ok(product);
});

app.MapDelete("/api/inventory/products/{productId:guid}", (Guid productId, InventoryDb db) =>
{
    var deleted = db.DeleteProduct(productId);
    return deleted ? Results.Ok(new { message = "Producto eliminado" }) : Results.NotFound(new { message = "Producto no encontrado" });
});

app.Run();

public partial class Program { }

record CreateInventoryProductRequest(string Sku, string Name, string Category, decimal Price, string Image, List<string> Colors, List<string> Measures, int Available, int Reserved, string SupplierName);
record UpdateInventoryProductRequest(string? Sku, string? Name, string? Category, decimal? Price, string? Image, List<string>? Colors, List<string>? Measures, int? Available, int? Reserved, string? SupplierName);
record InventoryProductResponse(Guid ProductId, string Sku, string Name, string Category, decimal Price, string Image, List<string> Colors, List<string> Measures, int Available, int Reserved, string SupplierName, DateTime CreatedAt, DateTime UpdatedAt);

sealed class InventoryDb
{
    private const string DefaultProductImage = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=900&q=80";
    private const string SofaOsloImage = "https://images.unsplash.com/photo-1555041469-a586c61ea9bc?auto=format&fit=crop&w=900&q=80";
    private const string MesaLunaImage = "https://images.unsplash.com/photo-1604578762246-41134e37f9cc?auto=format&fit=crop&w=900&q=80";
    private const string ModularNeoImage = "https://images.unsplash.com/photo-1493663284031-b7e3aaa4cab7?auto=format&fit=crop&w=900&q=80";
    private const string DeskFocusImage = "https://images.unsplash.com/photo-1518455027359-f3f8164ba6bd?auto=format&fit=crop&w=900&q=80";

    private readonly string _connectionString;

    public InventoryDb(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Initialize()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS inventory_products (
                product_id UUID PRIMARY KEY,
                sku TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL,
                category TEXT NOT NULL,
                price NUMERIC(18,2) NOT NULL,
                image TEXT NOT NULL,
                colors TEXT NOT NULL,
                measures TEXT NOT NULL,
                available INTEGER NOT NULL,
                reserved INTEGER NOT NULL,
                supplier_name TEXT NOT NULL,
                created_at TIMESTAMPTZ NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL
            );
        ";
        command.ExecuteNonQuery();

        using var imageColumnCommand = connection.CreateCommand();
        imageColumnCommand.CommandText = $@"
            ALTER TABLE inventory_products
            ADD COLUMN IF NOT EXISTS image TEXT NOT NULL DEFAULT '{DefaultProductImage}';
        ";
        imageColumnCommand.ExecuteNonQuery();
    }

    public void Seed()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM inventory_products;";
        var count = Convert.ToInt32(countCommand.ExecuteScalar());
        if (count > 0)
        {
            return;
        }

        InsertSeedProduct(connection, Guid.Parse("22222222-2222-2222-2222-222222222221"), "SOFA-OSLO-001", "Sofá Nórdico Oslo", "salas", 2499m, SofaOsloImage, new[] { "Arena", "Grafito", "Oliva" }, new[] { "2.10m", "2.40m" }, 12, 2, "Muebles Nórdicos");
        InsertSeedProduct(connection, Guid.Parse("22222222-2222-2222-2222-222222222222"), "MESA-LUNA-001", "Mesa Comedor Luna", "comedores", 1899m, MesaLunaImage, new[] { "Nogal", "Roble" }, new[] { "6 puestos", "8 puestos" }, 8, 1, "Diseños Luna" );
        InsertSeedProduct(connection, Guid.Parse("22222222-2222-2222-2222-222222222223"), "MOD-NEO-001", "Modular Neo", "salas", 3199m, ModularNeoImage, new[] { "Perla", "Azul humo" }, new[] { "3 módulos", "4 módulos" }, 5, 1, "Modulares Urban" );
        InsertSeedProduct(connection, Guid.Parse("22222222-2222-2222-2222-222222222224"), "DESK-FOCUS-001", "Escritorio Focus", "oficina", 1299m, DeskFocusImage, new[] { "Blanco", "Roble" }, new[] { "120 cm", "150 cm" }, 15, 0, "Oficinas Pro" );
    }

    public void UpdateSeedProductImages()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        UpdateProductImage(connection, "SOFA-OSLO-001", SofaOsloImage);
        UpdateProductImage(connection, "MESA-LUNA-001", MesaLunaImage);
        UpdateProductImage(connection, "MOD-NEO-001", ModularNeoImage);
        UpdateProductImage(connection, "DESK-FOCUS-001", DeskFocusImage);
    }

    public List<InventoryProductResponse> GetProducts()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT product_id, sku, name, category, price, image, colors, measures, available, reserved, supplier_name, created_at, updated_at
            FROM inventory_products
            ORDER BY created_at DESC;
        ";

        using var reader = command.ExecuteReader();
        var products = new List<InventoryProductResponse>();
        while (reader.Read())
        {
            products.Add(MapProduct(reader));
        }

        return products;
    }

    public InventoryProductResponse? GetProductById(Guid productId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT product_id, sku, name, category, price, image, colors, measures, available, reserved, supplier_name, created_at, updated_at
            FROM inventory_products
            WHERE product_id = @productId;
        ";
        command.Parameters.AddWithValue("productId", productId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapProduct(reader) : null;
    }

    public InventoryProductResponse CreateProduct(CreateInventoryProductRequest request)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var productId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO inventory_products (product_id, sku, name, category, price, image, colors, measures, available, reserved, supplier_name, created_at, updated_at)
            VALUES (@productId, @sku, @name, @category, @price, @image, @colors, @measures, @available, @reserved, @supplierName, @createdAt, @updatedAt);
        ";
        command.Parameters.AddWithValue("productId", productId);
        command.Parameters.AddWithValue("sku", request.Sku);
        command.Parameters.AddWithValue("name", request.Name);
        command.Parameters.AddWithValue("category", request.Category);
        command.Parameters.AddWithValue("price", request.Price);
        command.Parameters.AddWithValue("image", request.Image ?? string.Empty);
        command.Parameters.AddWithValue("colors", string.Join('|', request.Colors ?? new List<string>()));
        command.Parameters.AddWithValue("measures", string.Join('|', request.Measures ?? new List<string>()));
        command.Parameters.AddWithValue("available", request.Available);
        command.Parameters.AddWithValue("reserved", request.Reserved);
        command.Parameters.AddWithValue("supplierName", request.SupplierName ?? string.Empty);
        command.Parameters.AddWithValue("createdAt", now);
        command.Parameters.AddWithValue("updatedAt", now);
        command.ExecuteNonQuery();

        return new InventoryProductResponse(productId, request.Sku, request.Name, request.Category, request.Price, request.Image ?? string.Empty, request.Colors ?? new List<string>(), request.Measures ?? new List<string>(), request.Available, request.Reserved, request.SupplierName ?? string.Empty, now, now);
    }

    public InventoryProductResponse? UpdateProduct(Guid productId, UpdateInventoryProductRequest request)
    {
        var existing = GetProductById(productId);
        if (existing is null)
        {
            return null;
        }

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var updated = new InventoryProductResponse(
            productId,
            request.Sku ?? existing.Sku,
            request.Name ?? existing.Name,
            request.Category ?? existing.Category,
            request.Price ?? existing.Price,
            request.Image ?? existing.Image,
            request.Colors ?? existing.Colors,
            request.Measures ?? existing.Measures,
            request.Available ?? existing.Available,
            request.Reserved ?? existing.Reserved,
            request.SupplierName ?? existing.SupplierName,
            existing.CreatedAt,
            DateTime.UtcNow);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE inventory_products
            SET sku = @sku,
                name = @name,
                category = @category,
                price = @price,
                image = @image,
                colors = @colors,
                measures = @measures,
                available = @available,
                reserved = @reserved,
                supplier_name = @supplierName,
                updated_at = @updatedAt
            WHERE product_id = @productId;
        ";
        command.Parameters.AddWithValue("sku", updated.Sku);
        command.Parameters.AddWithValue("name", updated.Name);
        command.Parameters.AddWithValue("category", updated.Category);
        command.Parameters.AddWithValue("price", updated.Price);
        command.Parameters.AddWithValue("image", updated.Image);
        command.Parameters.AddWithValue("colors", string.Join('|', updated.Colors));
        command.Parameters.AddWithValue("measures", string.Join('|', updated.Measures));
        command.Parameters.AddWithValue("available", updated.Available);
        command.Parameters.AddWithValue("reserved", updated.Reserved);
        command.Parameters.AddWithValue("supplierName", updated.SupplierName);
        command.Parameters.AddWithValue("updatedAt", updated.UpdatedAt);
        command.Parameters.AddWithValue("productId", productId);
        command.ExecuteNonQuery();

        return updated;
    }

    public bool DeleteProduct(Guid productId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM inventory_products WHERE product_id = @productId;";
        command.Parameters.AddWithValue("productId", productId);
        return command.ExecuteNonQuery() > 0;
    }

    private static void InsertSeedProduct(NpgsqlConnection connection, Guid productId, string sku, string name, string category, decimal price, string image, IEnumerable<string> colors, IEnumerable<string> measures, int available, int reserved, string supplierName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO inventory_products (product_id, sku, name, category, price, image, colors, measures, available, reserved, supplier_name, created_at, updated_at)
            VALUES (@productId, @sku, @name, @category, @price, @image, @colors, @measures, @available, @reserved, @supplierName, @createdAt, @updatedAt);
        ";
        var now = DateTime.UtcNow;
        command.Parameters.AddWithValue("productId", productId);
        command.Parameters.AddWithValue("sku", sku);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("category", category);
        command.Parameters.AddWithValue("price", price);
        command.Parameters.AddWithValue("image", image);
        command.Parameters.AddWithValue("colors", string.Join('|', colors));
        command.Parameters.AddWithValue("measures", string.Join('|', measures));
        command.Parameters.AddWithValue("available", available);
        command.Parameters.AddWithValue("reserved", reserved);
        command.Parameters.AddWithValue("supplierName", supplierName);
        command.Parameters.AddWithValue("createdAt", now);
        command.Parameters.AddWithValue("updatedAt", now);
        command.ExecuteNonQuery();
    }

    private static void UpdateProductImage(NpgsqlConnection connection, string sku, string image)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE inventory_products
            SET image = @image,
                updated_at = @updatedAt
            WHERE sku = @sku
              AND (image IS NULL OR image = '' OR image LIKE 'https://via.placeholder.com/%');
        ";
        command.Parameters.AddWithValue("sku", sku);
        command.Parameters.AddWithValue("image", image);
        command.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);
        command.ExecuteNonQuery();
    }

    private static InventoryProductResponse MapProduct(NpgsqlDataReader reader)
    {
        return new InventoryProductResponse(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDecimal(4),
            reader.GetString(5),
            SplitList(reader.GetString(6)),
            SplitList(reader.GetString(7)),
            reader.GetInt32(8),
            reader.GetInt32(9),
            reader.GetString(10),
            reader.GetDateTime(11),
            reader.GetDateTime(12));
    }

    private static List<string> SplitList(string raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
