using Npgsql;
using Xunit;

namespace InventoryService.Tests;

public sealed class InventoryDbIntegrationTests : IClassFixture<PostgresContainerFixture>, IDisposable
{
    private readonly InventoryDb _db;
    private readonly string _connectionString;

    public InventoryDbIntegrationTests(PostgresContainerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _db = new InventoryDb(_connectionString);
        _db.Initialize();
        // PREPARAR: tabla limpia antes de cada prueba (la tabla se crea con Initialize, aqui borramos filas)
        TruncateTable();
    }

    public void Dispose()
    {
        TruncateTable();
    }

    private void TruncateTable()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE inventory_products RESTART IDENTITY CASCADE;";
        command.ExecuteNonQuery();
    }

    // --- PRUEBA 1: crear producto y consultarlo por id ---
    [Fact(DisplayName = "Integracion: CreateProduct persiste y GetProductById retorna el producto")]
    public void CreateProduct_ThenGetById_ReturnsCreatedProduct()
    {
        // Arrange
        var request = new CreateInventoryProductRequest(
            Sku: "SOFA-TEST-001",
            Name: "Sofá de prueba",
            Category: "salas",
            Price: 1500m,
            Image: "img.jpg",
            Colors: new List<string> { "Gris", "Negro" },
            Measures: new List<string> { "2.10m" },
            Available: 5,
            Reserved: 0,
            SupplierName: "Proveedor X");

        // Act
        var created = _db.CreateProduct(request);
        var fetched = _db.GetProductById(created.ProductId);

        // Assert: dato real persistido en Postgres
        Assert.NotNull(fetched);
        Assert.Equal("SOFA-TEST-001", fetched!.Sku);
        Assert.Equal("Sofá de prueba", fetched.Name);
        Assert.Equal(1500m, fetched.Price);
        Assert.Equal(new List<string> { "Gris", "Negro" }, fetched.Colors);
    }

    // --- PRUEBA 2: actualizar producto mantiene campos no enviados ---
    [Fact(DisplayName = "Integracion: UpdateProduct conserva campos no enviados (null)")]
    public void UpdateProduct_WithPartialRequest_KeepsExistingFields()
    {
        // Arrange
        var original = _db.CreateProduct(new CreateInventoryProductRequest(
            "MESA-TEST-001", "Mesa Original", "comedores", 999m, "orig.jpg",
            new List<string> { "Roble" }, new List<string> { "6 puestos" },
            10, 1, "Proveedor Y"));

        // Act: cambiamos solo precio, lo demas null
        var updated = _db.UpdateProduct(original.ProductId,
            new UpdateInventoryProductRequest(null, null, null, 1299m, null, null, null, null, null, null));

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(1299m, updated!.Price);
        Assert.Equal("Mesa Original", updated.Name);    // se conservo
        Assert.Equal("comedores", updated.Category);     // se conservo
        Assert.Equal("Proveedor Y", updated.SupplierName); // se conservo
    }

    // --- PRUEBA 3: eliminar producto retorna true y desaparece de la BD ---
    [Fact(DisplayName = "Integracion: DeleteProduct elimina y GetProductById retorna null")]
    public void DeleteProduct_RemovesFromDatabase()
    {
        // Arrange
        var created = _db.CreateProduct(new CreateInventoryProductRequest(
            "DEL-TEST-001", "Para borrar", "oficina", 500m, "img.jpg",
            new List<string>(), new List<string>(),
            1, 0, "Tmp"));

        // Act
        var deleted = _db.DeleteProduct(created.ProductId);

        // Assert
        Assert.True(deleted);
        Assert.Null(_db.GetProductById(created.ProductId));
    }

    // --- PRUEBA 4: eliminar id inexistente retorna false ---
    [Fact(DisplayName = "Integracion: DeleteProduct con id inexistente retorna false")]
    public void DeleteProduct_WithUnknownId_ReturnsFalse()
    {
        // Act
        var deleted = _db.DeleteProduct(Guid.NewGuid());

        // Assert
        Assert.False(deleted);
    }

    // --- PRUEBA 5: GetProducts retorna en orden created_at desc ---
    [Fact(DisplayName = "Integracion: GetProducts retorna productos ordenados por fecha de creacion")]
    public void GetProducts_ReturnsAllProductsOrderedByCreatedAtDesc()
    {
        // Arrange
        _db.CreateProduct(new CreateInventoryProductRequest(
            "A", "Primero", "cat", 100m, "", new(), new(), 1, 0, "p"));
        Thread.Sleep(10);
        _db.CreateProduct(new CreateInventoryProductRequest(
            "B", "Segundo", "cat", 200m, "", new(), new(), 1, 0, "p"));

        // Act
        var products = _db.GetProducts();

        // Assert
        Assert.Equal(2, products.Count);
        Assert.Equal("Segundo", products[0].Name); // mas reciente primero
        Assert.Equal("Primero", products[1].Name);
    }
}
