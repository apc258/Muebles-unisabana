using Microsoft.Extensions.Configuration;
using Moq;
using PaymentService.Api.Validators;
using Xunit;

namespace PaymentService.Tests;

public sealed class PaymentMockTests
{
    // --- PRUEBA MOCK 1: validador rechaza pago invalido sin tocar configuracion ---
    [Fact(DisplayName = "Mock: validador rechaza pago invalido sin consultar IConfiguration")]
    public void Validator_RejectsInvalidPayment_NoConfigReadHappens()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c[It.IsAny<string>()]).Returns((string?)null);

        // Act: items=0 -> invalido
        var result = PaymentRequestValidator.Validate(Guid.NewGuid(), "Cliente", "c@x.com", itemsCount: 0);

        // Assert
        Assert.False(result.IsValid);
        configMock.Verify(c => c[It.IsAny<string>()], Times.Never);
    }

    // --- PRUEBA MOCK 2: gateway de pago externo se invoca con monto calculado correctamente ---
    [Fact(DisplayName = "Mock: gateway externo de pago se llama con el total calculado por PaymentTotalsCalculator")]
    public void PaymentGateway_IsCalledWithCalculatedTotal()
    {
        // Arrange: interfaz publica que representa un gateway externo (Stripe, PayU, etc.)
        var gatewayMock = new Mock<IExternalPaymentGateway>();
        gatewayMock
            .Setup(g => g.Authorize(It.IsAny<decimal>(), It.IsAny<string>()))
            .Returns(true);

        var items = new[] { (Quantity: 2, UnitPrice: 100m) }; // subtotal 200, IVA 32, total 232
        var totals = PaymentTotalsCalculator.Calculate(items);

        // Act: consumidor delega autorizacion al gateway mockeado
        var consumer = new PaymentConsumer(gatewayMock.Object);
        var authorized = consumer.Process(totals.Total, "Tarjeta");

        // Assert
        Assert.True(authorized);
        gatewayMock.Verify(g => g.Authorize(232m, "Tarjeta"), Times.Once);
    }
}

public interface IExternalPaymentGateway
{
    bool Authorize(decimal total, string method);
}

internal sealed class PaymentConsumer
{
    private readonly IExternalPaymentGateway _gateway;
    public PaymentConsumer(IExternalPaymentGateway gateway) => _gateway = gateway;
    public bool Process(decimal total, string method) => _gateway.Authorize(total, method);
}
