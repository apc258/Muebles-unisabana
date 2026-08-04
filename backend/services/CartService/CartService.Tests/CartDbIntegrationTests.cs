using Npgsql;
using Xunit;

namespace CartService.Tests;

public sealed class CartDbIntegrationTests : IClassFixture<PostgresContainerFixture>, IDisposable
{
    private readonly CartDb _db;
    private readonly string _connectionString;
    private static readonly Guid CustomerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Product1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Product2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public CartDbIntegrationTests(PostgresContainerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _db = new CartDb(_connectionString);
        _db.Initialize();
        TruncateTables();
    }

    public void Dispose() => TruncateTables();

    private void TruncateTables()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE cart_items, carts RESTART IDENTITY CASCADE;";
        command.ExecuteNonQuery();
    }

    // --- PRUEBA 1: GetOrCreateCart crea carrito nuevo y la segunda llamada devuelve el mismo ---
    [Fact(DisplayName = "Integracion: GetOrCreateCart crea cart la primera vez y devuelve el mismo despues")]
    public void GetOrCreateCart_CreatesNewThenReturnsExisting()
    {
        // Act
        var first = _db.GetOrCreateCart(CustomerA);
        var second = _db.GetOrCreateCart(CustomerA);

        // Assert
        Assert.Equal(CustomerA, first.CustomerId);
        Assert.Equal(first.Id, second.Id); // mismo cart, no duplicado
    }

    // --- PRUEBA 2: AddItem persiste items y calcula subtotal + IVA 16% en GetCartByCustomerId ---
    [Fact(DisplayName = "Integracion: AddItem persiste items y GetCartByCustomerId calcula IVA 16%")]
    public void AddItem_PersistsAndCalculatesTotalWithIva()
    {
        // Arrange
        var cart = _db.GetOrCreateCart(CustomerA);

        // Act: 2 productos
        _db.AddItem(cart.Id, Product1, 2, 1000m, "Producto A");
        _db.AddItem(cart.Id, Product2, 1, 500m, "Producto B");

        // Assert
        var fetched = _db.GetCartByCustomerId(CustomerA);
        Assert.NotNull(fetched);
        Assert.Equal(2, fetched!.Items.Count);
        // subtotal = 2*1000 + 1*500 = 2500; IVA 16% = 400; total = 2900
        Assert.Equal(2900m, fetched.TotalAmount);
    }

    // --- PRUEBA 3: AddItem duplicado (mismo cart_id + product_id) actualiza cantidad via ON CONFLICT ---
    [Fact(DisplayName = "Integracion: AddItem duplicado suma cantidades (ON CONFLICT DO UPDATE)")]
    public void AddItem_WithSameProduct_IncrementsQuantity()
    {
        // Arrange
        var cart = _db.GetOrCreateCart(CustomerA);

        // Act: agregamos el mismo producto dos veces
        _db.AddItem(cart.Id, Product1, 2, 1000m, "Producto A");
        _db.AddItem(cart.Id, Product1, 3, 1000m, "Producto A");

        // Assert: una sola fila con quantity = 2 + 3 = 5
        var fetched = _db.GetCartByCustomerId(CustomerA);
        Assert.NotNull(fetched);
        Assert.Single(fetched!.Items);
        Assert.Equal(5, fetched.Items[0].Quantity);
    }

    // --- PRUEBA 4: RemoveItem elimina solo el item indicado, no los demas ---
    [Fact(DisplayName = "Integracion: RemoveItem elimina solo el producto indicado")]
    public void RemoveItem_RemovesOnlySpecifiedItem()
    {
        // Arrange
        var cart = _db.GetOrCreateCart(CustomerA);
        _db.AddItem(cart.Id, Product1, 1, 100m, "A");
        _db.AddItem(cart.Id, Product2, 1, 200m, "B");

        // Act
        _db.RemoveItem(CustomerA, Product1);

        // Assert
        var fetched = _db.GetCartByCustomerId(CustomerA);
        Assert.NotNull(fetched);
        Assert.Single(fetched!.Items);
        Assert.Equal(Product2, fetched.Items[0].ProductId);
    }

    // --- PRUEBA 5: ClearCart vacia los items pero conserva el cart ---
    [Fact(DisplayName = "Integracion: ClearCart elimina todos los items pero conserva el cart")]
    public void ClearCart_RemovesItemsButKeepsCart()
    {
        // Arrange
        var cart = _db.GetOrCreateCart(CustomerA);
        _db.AddItem(cart.Id, Product1, 1, 100m, "A");
        _db.AddItem(cart.Id, Product2, 1, 200m, "B");

        // Act
        _db.ClearCart(CustomerA);

        // Assert
        var fetched = _db.GetCartByCustomerId(CustomerA);
        Assert.NotNull(fetched);                    // el cart sigue existiendo
        Assert.Equal(cart.Id, fetched!.Id);          // y es el mismo id
        Assert.Empty(fetched.Items);                 // pero sin items
        Assert.Equal(0m, fetched.TotalAmount);
    }
}
