using Npgsql;
using Xunit;

namespace OrderService.Tests;

public sealed class OrderDbIntegrationTests : IClassFixture<PostgresContainerFixture>, IDisposable
{
    private readonly OrderDb _db;
    private readonly string _connectionString;
    private static readonly Guid CustomerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CustomerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Product1 = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public OrderDbIntegrationTests(PostgresContainerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _db = new OrderDb(_connectionString);
        _db.Initialize();
        TruncateTables();
    }

    public void Dispose() => TruncateTables();

    private void TruncateTables()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE order_items, orders RESTART IDENTITY CASCADE;";
        command.ExecuteNonQuery();
    }

    // --- PRUEBA 1: CreateOrder calcula subtotal/tax/total y persiste items ---
    [Fact(DisplayName = "Integracion: CreateOrder persiste orden + items con subtotal/tax/total correctos")]
    public void CreateOrder_PersistsOrderWithCorrectTotals()
    {
        // Arrange
        var request = new CreateOrderRequest(CustomerA, new List<CreateOrderItemRequest>
        {
            new(Product1, 2, 1000m) // 2 * 1000 = 2000; tax=320; total=2320
        });

        // Act
        var created = _db.CreateOrder(request);
        var fetched = _db.GetOrder(created.OrderId);

        // Assert
        Assert.NotNull(fetched);
        Assert.Equal(CustomerA, fetched!.CustomerId);
        Assert.Equal("Created", fetched.Status);
        Assert.Equal(2000m, fetched.Subtotal);
        Assert.Equal(320m, fetched.Tax);
        Assert.Equal(2320m, fetched.Total);
        Assert.Single(fetched.Items);
        Assert.Equal(2, fetched.Items[0].Quantity);
    }

    // --- PRUEBA 2: GetOrdersByCustomerId filtra correctamente ---
    [Fact(DisplayName = "Integracion: GetOrdersByCustomerId solo retorna ordenes del cliente solicitado")]
    public void GetOrdersByCustomerId_FiltersCorrectly()
    {
        // Arrange
        _db.CreateOrder(new CreateOrderRequest(CustomerA, new List<CreateOrderItemRequest> { new(Product1, 1, 100m) }));
        _db.CreateOrder(new CreateOrderRequest(CustomerB, new List<CreateOrderItemRequest> { new(Product1, 1, 200m) }));
        _db.CreateOrder(new CreateOrderRequest(CustomerA, new List<CreateOrderItemRequest> { new(Product1, 1, 300m) }));

        // Act
        var ordersA = _db.GetOrdersByCustomerId(CustomerA);
        var ordersB = _db.GetOrdersByCustomerId(CustomerB);

        // Assert
        Assert.Equal(2, ordersA.Count);
        Assert.All(ordersA, o => Assert.Equal(CustomerA, o.CustomerId));
        Assert.Single(ordersB);
        Assert.Equal(CustomerB, ordersB[0].CustomerId);
    }

    // --- PRUEBA 3: UpdateOrder cambia el status y se persiste ---
    [Fact(DisplayName = "Integracion: UpdateOrder cambia el status y actualiza updated_at")]
    public void UpdateOrder_ChangesStatus()
    {
        // Arrange
        var order = _db.CreateOrder(new CreateOrderRequest(CustomerA, new List<CreateOrderItemRequest> { new(Product1, 1, 100m) }));

        // Act
        var updated = _db.UpdateOrder(order.OrderId, new UpdateOrderRequest("Paid"));

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("Paid", updated!.Status);
    }

    // --- PRUEBA 4: DeleteOrder elimina con cascade los order_items ---
    [Fact(DisplayName = "Integracion: DeleteOrder elimina la orden y sus items (cascade)")]
    public void DeleteOrder_RemovesOrderAndItems()
    {
        // Arrange
        var order = _db.CreateOrder(new CreateOrderRequest(CustomerA,
            new List<CreateOrderItemRequest> { new(Product1, 2, 100m) }));

        // Act
        var deleted = _db.DeleteOrder(order.OrderId);

        // Assert
        Assert.True(deleted);
        Assert.Null(_db.GetOrder(order.OrderId));

        // Verificamos directamente que tampoco quedaron order_items huerfanos
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM order_items WHERE order_id = @id;";
        command.Parameters.AddWithValue("id", order.OrderId);
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
    }

    // --- PRUEBA 5: UpdateOrder con id inexistente retorna null ---
    [Fact(DisplayName = "Integracion: UpdateOrder con id inexistente retorna null")]
    public void UpdateOrder_WithUnknownId_ReturnsNull()
    {
        var updated = _db.UpdateOrder(Guid.NewGuid(), new UpdateOrderRequest("Paid"));
        Assert.Null(updated);
    }
}
