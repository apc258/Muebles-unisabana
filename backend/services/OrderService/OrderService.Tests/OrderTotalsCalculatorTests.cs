using OrderService.Api.Validators;
using Xunit;

namespace OrderService.Tests;

public sealed class OrderTotalsCalculatorTests
{
    [Fact(DisplayName = "Unitaria: calculo de totales suma cantidades por precio + IVA 16%")]
    public void Calculate_WithTwoItems_ReturnsSubtotalTaxAndTotal()
    {
        // Arrange: 2 productos => subtotal 2499 + 1899 = 4398; tax = 703.68; total = 5101.68
        var items = new[]
        {
            (Quantity: 1, UnitPrice: 2499m),
            (Quantity: 1, UnitPrice: 1899m)
        };

        // Act
        var totals = OrderTotalsCalculator.Calculate(items);

        // Assert
        Assert.Equal(4398m, totals.Subtotal);
        Assert.Equal(703.68m, totals.Tax);
        Assert.Equal(5101.68m, totals.Total);
    }

    [Theory(DisplayName = "Unitaria: calculo de totales con cantidades multiples")]
    [InlineData(2, 1000, 2000, 320, 2320)]
    [InlineData(3, 500, 1500, 240, 1740)]
    [InlineData(1, 100, 100, 16, 116)]
    public void Calculate_WithVariousQuantities_ProducesExpectedTotals(int qty, decimal price, decimal expSubtotal, decimal expTax, decimal expTotal)
    {
        var totals = OrderTotalsCalculator.Calculate(new[] { (qty, price) });

        Assert.Equal(expSubtotal, totals.Subtotal);
        Assert.Equal(expTax, totals.Tax);
        Assert.Equal(expTotal, totals.Total);
    }

    [Fact(DisplayName = "Unitaria: items vacios retornan totales en cero")]
    public void Calculate_WithEmptyItems_ReturnsZero()
    {
        var totals = OrderTotalsCalculator.Calculate(Array.Empty<(int, decimal)>());

        Assert.Equal(0m, totals.Subtotal);
        Assert.Equal(0m, totals.Tax);
        Assert.Equal(0m, totals.Total);
    }

    [Fact(DisplayName = "Unitaria: IVA se redondea hacia arriba en .5 (MidpointRounding.AwayFromZero)")]
    public void Calculate_RoundsTaxAwayFromZero()
    {
        // subtotal 100.05 → tax = 16.008 → redondea a 16.01
        var totals = OrderTotalsCalculator.Calculate(new[] { (1, 100.05m) });
        Assert.Equal(16.01m, totals.Tax);
    }
}
