using InventoryService.Api.Validators;
using Xunit;

namespace InventoryService.Tests;

public sealed class InventoryProductValidatorTests
{
    [Fact(DisplayName = "Unitaria: producto con todos los campos validos es valido")]
    public void Validate_WhenAllFieldsValid_ReturnsValid()
    {
        var result = InventoryProductValidator.Validate("SKU-1", "Producto", "cat", 100m);
        Assert.True(result.IsValid);
    }

    [Theory(DisplayName = "Unitaria: producto con sku/name/category vacio o price <= 0 es invalido")]
    [InlineData(null, "Producto", "cat", 100)]
    [InlineData("", "Producto", "cat", 100)]
    [InlineData("SKU-1", null, "cat", 100)]
    [InlineData("SKU-1", "", "cat", 100)]
    [InlineData("SKU-1", "Producto", null, 100)]
    [InlineData("SKU-1", "Producto", " ", 100)]
    [InlineData("SKU-1", "Producto", "cat", 0)]
    [InlineData("SKU-1", "Producto", "cat", -1)]
    public void Validate_WhenAnyFieldInvalid_ReturnsInvalid(string? sku, string? name, string? category, decimal price)
    {
        var result = InventoryProductValidator.Validate(sku, name, category, price);
        Assert.False(result.IsValid);
        Assert.Equal("sku, name, category y price son obligatorios", result.ErrorMessage);
    }

    [Theory(DisplayName = "Unitaria: SplitList divide por pipe y limpia entradas vacias")]
    [InlineData("Gris|Negro|Blanco", new[] { "Gris", "Negro", "Blanco" })]
    [InlineData(" Gris | Negro ", new[] { "Gris", "Negro" })]
    [InlineData("Solo", new[] { "Solo" })]
    public void SplitList_DividesByPipeAndTrims(string raw, string[] expected)
    {
        var result = InventoryProductValidator.SplitList(raw);
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "Unitaria: SplitList con texto vacio o nulo retorna lista vacia")]
    [InlineData("")]
    [InlineData("  ")]
    public void SplitList_WithEmpty_ReturnsEmptyList(string raw)
    {
        var result = InventoryProductValidator.SplitList(raw);
        Assert.Empty(result);
    }
}
