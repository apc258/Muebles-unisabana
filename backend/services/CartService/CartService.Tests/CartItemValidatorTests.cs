using CartService.Api.Validators;
using Xunit;

namespace CartService.Tests;

public sealed class CartItemValidatorTests
{
    [Fact(DisplayName = "Unitaria: item valido con quantity > 0 y unitPrice >= 0 retorna valido")]
    public void Validate_WhenValid_ReturnsValid()
    {
        var result = CartItemValidator.Validate(2, 100m);
        Assert.True(result.IsValid);
    }

    [Theory(DisplayName = "Unitaria: item con quantity <= 0 o unitPrice < 0 retorna invalido")]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(1, -0.01)]
    [InlineData(-5, -10)]
    public void Validate_WhenInvalid_ReturnsInvalid(int qty, decimal price)
    {
        var result = CartItemValidator.Validate(qty, price);
        Assert.False(result.IsValid);
        Assert.Equal("Cantidad o precio inválidos.", result.ErrorMessage);
    }

    [Fact(DisplayName = "Unitaria: unitPrice = 0 es valido (producto gratis o promocion)")]
    public void Validate_WithZeroPrice_IsValid()
    {
        var result = CartItemValidator.Validate(1, 0m);
        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "Unitaria: calculo de totales del carrito con IVA 16%")]
    public void CalculateTotals_TwoItems_AppliesIva16Percent()
    {
        var totals = CartItemValidator.CalculateTotals(new[]
        {
            (Quantity: 2, UnitPrice: 1000m),
            (Quantity: 1, UnitPrice: 500m)
        });

        Assert.Equal(2500m, totals.Subtotal);
        Assert.Equal(400m, totals.Tax);
        Assert.Equal(2900m, totals.Total);
    }

    [Theory(DisplayName = "Unitaria: calculo de totales con varios escenarios")]
    [InlineData(1, 100, 100, 16, 116)]
    [InlineData(0, 100, 0, 0, 0)]
    [InlineData(5, 200, 1000, 160, 1160)]
    public void CalculateTotals_VariousScenarios(int qty, decimal price, decimal expSub, decimal expTax, decimal expTotal)
    {
        var totals = CartItemValidator.CalculateTotals(new[] { (qty, price) });
        Assert.Equal(expSub, totals.Subtotal);
        Assert.Equal(expTax, totals.Tax);
        Assert.Equal(expTotal, totals.Total);
    }

    [Fact(DisplayName = "Unitaria: carrito vacio produce totales en cero")]
    public void CalculateTotals_EmptyCart_ReturnsZero()
    {
        var totals = CartItemValidator.CalculateTotals(Array.Empty<(int, decimal)>());
        Assert.Equal(0m, totals.Subtotal);
        Assert.Equal(0m, totals.Tax);
        Assert.Equal(0m, totals.Total);
    }
}
