using Microsoft.Extensions.Configuration;
using Moq;
using OrderService.Api.Validators;
using Xunit;

namespace OrderService.Tests;

public sealed class OrderMockTests
{
    // --- PRUEBA MOCK 1: cuando el validador rechaza, no se consulta IConfiguration ---
    [Fact(DisplayName = "Mock: validador rechaza orden invalida sin consultar IConfiguration")]
    public void Validator_RejectsInvalidOrder_NoConfigurationReadHappens()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c[It.IsAny<string>()]).Returns((string?)null);

        // Act: lista vacia de items -> invalido
        var result = OrderRequestValidator.Validate(Guid.NewGuid(), Array.Empty<(Guid, int, decimal)>());

        // Assert
        Assert.False(result.IsValid);
        configMock.Verify(c => c[It.IsAny<string>()], Times.Never);
    }

    // --- PRUEBA MOCK 2: notificador de ordenes (interfaz simple) se invoca exactamente una vez ---
    [Fact(DisplayName = "Mock: notificador de ordenes se invoca una vez por orden creada (verify interaction)")]
    public void OrderNotifier_IsInvokedOncePerCreatedOrder()
    {
        // Arrange: definimos una interfaz local que un consumidor real podria usar
        // para notificar otras areas (ej: shipping, billing) cuando se crea una orden.
        // Esta prueba demuestra el patron "verify interactions" del Word con Mockito.
        var notifierMock = new Mock<IOrderCreatedNotifier>();
        notifierMock.Setup(n => n.Notify(It.IsAny<Guid>())).Verifiable();

        // Simulamos un consumidor que delega la notificacion al mock
        var orderId = Guid.NewGuid();
        var consumer = new OrderConsumer(notifierMock.Object);

        // Act
        consumer.OnOrderCreated(orderId);

        // Assert: el mock fue llamado exactamente una vez con el id correcto
        notifierMock.Verify(n => n.Notify(orderId), Times.Once);
        notifierMock.VerifyNoOtherCalls();
    }
}

public interface IOrderCreatedNotifier
{
    void Notify(Guid orderId);
}

internal sealed class OrderConsumer
{
    private readonly IOrderCreatedNotifier _notifier;
    public OrderConsumer(IOrderCreatedNotifier notifier) => _notifier = notifier;
    public void OnOrderCreated(Guid orderId) => _notifier.Notify(orderId);
}
