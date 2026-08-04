using CatalogService.Domain.Entities;
using Xunit;

namespace CatalogService.Tests;

public sealed class ProductValidationTests
{
    [Fact(DisplayName = "Unitaria: producto completo retorna valido")]
    public void Validate_WhenProductIsComplete_ReturnsValid()
    {
        // Arrange
        var product = ProductTestData.Product().Build();

        // Act
        var result = product.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory(DisplayName = "Unitaria: precio cero o negativo retorna error de precio")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenPriceIsZeroOrNegative_ReturnsPriceError(decimal price)
    {
        // Arrange
        var product = ProductTestData.Product()
            .WithPrice(price)
            .Build();

        // Act
        var result = product.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("El precio debe ser mayor que cero.", result.Errors);
    }

    [Theory(DisplayName = "Unitaria: nombre vacio o en blanco retorna error de nombre")]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WhenNameIsBlank_ReturnsNameError(string name)
    {
        // Arrange
        var product = ProductTestData.Product()
            .WithName(name)
            .Build();

        // Act
        var result = product.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("El nombre del producto es obligatorio.", result.Errors);
    }
}
