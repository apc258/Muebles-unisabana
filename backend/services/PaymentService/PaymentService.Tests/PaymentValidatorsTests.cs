using PaymentService.Api.Validators;
using Xunit;

namespace PaymentService.Tests;

public sealed class PaymentValidatorsTests
{
    [Fact(DisplayName = "Unitaria: pago con todos los campos validos es valido")]
    public void Validate_WhenAllValid_ReturnsValid()
    {
        var result = PaymentRequestValidator.Validate(
            Guid.NewGuid(), "Cliente", "cliente@x.com", itemsCount: 1);
        Assert.True(result.IsValid);
    }

    [Theory(DisplayName = "Unitaria: pago con campos invalidos es invalido")]
    [InlineData(0, "Cliente", "x@x.com", 1, "orderId vacio")]
    [InlineData(1, null, "x@x.com", 1, "customerName null")]
    [InlineData(1, "", "x@x.com", 1, "customerName vacio")]
    [InlineData(1, "Cliente", null, 1, "customerEmail null")]
    [InlineData(1, "Cliente", "", 1, "customerEmail vacio")]
    [InlineData(1, "Cliente", "x@x.com", 0, "items 0")]
    public void Validate_WhenInvalid_ReturnsInvalid(int orderMarker, string? name, string? email, int itemsCount, string _)
    {
        var orderId = orderMarker == 0 ? Guid.Empty : Guid.NewGuid();
        var result = PaymentRequestValidator.Validate(orderId, name, email, itemsCount);
        Assert.False(result.IsValid);
        Assert.Equal("orderId, customerName, customerEmail e items son obligatorios", result.ErrorMessage);
    }

    [Fact(DisplayName = "Unitaria: PaymentTotalsCalculator calcula correctamente con dos items")]
    public void Calculator_TwoItems_ReturnsCorrectTotals()
    {
        var totals = PaymentTotalsCalculator.Calculate(new[]
        {
            (Quantity: 1, UnitPrice: 2499m),
            (Quantity: 1, UnitPrice: 1899m)
        });

        Assert.Equal(4398m, totals.Subtotal);
        Assert.Equal(703.68m, totals.Tax);
        Assert.Equal(5101.68m, totals.Total);
    }

    [Theory(DisplayName = "Unitaria: PaymentTotalsCalculator aplica IVA 16% redondeado")]
    [InlineData(1, 100, 100, 16, 116)]
    [InlineData(3, 50, 150, 24, 174)]
    [InlineData(2, 1000, 2000, 320, 2320)]
    public void Calculator_VariousScenarios(int qty, decimal price, decimal expSub, decimal expTax, decimal expTotal)
    {
        var totals = PaymentTotalsCalculator.Calculate(new[] { (qty, price) });
        Assert.Equal(expSub, totals.Subtotal);
        Assert.Equal(expTax, totals.Tax);
        Assert.Equal(expTotal, totals.Total);
    }

    [Fact(DisplayName = "Unitaria: InvoiceNumberGenerator produce formato FAC-yyyyMMdd-XXXXXXXX")]
    public void InvoiceNumberGenerator_ProducesExpectedFormat()
    {
        var fixedDate = new DateTime(2026, 6, 16, 10, 0, 0, DateTimeKind.Utc);
        var paymentId = Guid.Parse("abc123ef-4567-4567-4567-456745674567");

        var invoiceNumber = InvoiceNumberGenerator.Generate(fixedDate, paymentId);

        Assert.Equal("FAC-20260616-ABC123EF", invoiceNumber);
    }

    [Fact(DisplayName = "Unitaria: InvoiceNumberGenerator usa los primeros 8 caracteres del paymentId en mayusculas")]
    public void InvoiceNumberGenerator_UsesFirstEightCharsInUpperCase()
    {
        var date = new DateTime(2025, 12, 31);
        var paymentId = Guid.Parse("deadbeef-0000-0000-0000-000000000000");

        var invoiceNumber = InvoiceNumberGenerator.Generate(date, paymentId);

        Assert.Equal("FAC-20251231-DEADBEEF", invoiceNumber);
    }
}
