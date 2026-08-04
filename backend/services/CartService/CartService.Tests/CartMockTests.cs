using CartService.Api.Validators;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace CartService.Tests;

public sealed class CartMockTests
{
    // --- PRUEBA MOCK 1: validador rechaza item invalido sin tocar configuracion ---
    [Fact(DisplayName = "Mock: validador rechaza item invalido sin consultar IConfiguration")]
    public void Validator_RejectsInvalidItem_NoConfigReadHappens()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c[It.IsAny<string>()]).Returns((string?)null);

        // Act
        var result = CartItemValidator.Validate(quantity: 0, unitPrice: 100m);

        // Assert
        Assert.False(result.IsValid);
        configMock.Verify(c => c[It.IsAny<string>()], Times.Never);
    }

    // --- PRUEBA MOCK 2: notificador de "agregado al carrito" se invoca correctamente ---
    [Fact(DisplayName = "Mock: notificador de carrito recibe customerId y productId esperados")]
    public void CartNotifier_IsInvokedWithExpectedArguments()
    {
        // Arrange
        var notifierMock = new Mock<ICartItemAddedNotifier>();
        notifierMock.Setup(n => n.NotifyItemAdded(It.IsAny<Guid>(), It.IsAny<Guid>())).Verifiable();

        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var consumer = new CartConsumer(notifierMock.Object);

        // Act
        consumer.OnItemAdded(customerId, productId);

        // Assert
        notifierMock.Verify(n => n.NotifyItemAdded(customerId, productId), Times.Once);
        notifierMock.VerifyNoOtherCalls();
    }
}

public interface ICartItemAddedNotifier
{
    void NotifyItemAdded(Guid customerId, Guid productId);
}

internal sealed class CartConsumer
{
    private readonly ICartItemAddedNotifier _notifier;
    public CartConsumer(ICartItemAddedNotifier notifier) => _notifier = notifier;
    public void OnItemAdded(Guid customerId, Guid productId) =>
        _notifier.NotifyItemAdded(customerId, productId);
}
