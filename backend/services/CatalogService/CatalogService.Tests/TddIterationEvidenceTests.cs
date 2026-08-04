using CatalogService.Domain.Entities;
using Xunit;

namespace CatalogService.Tests;

public sealed class TddIterationEvidenceTests
{
    [Fact(DisplayName = "TDD 1 Green: producto completo es valido")]
    public void ProductWithRequiredFieldsIsValid()
    {
        // Arrange
        var product = ProductTestData.Product().Build();

        // Act
        var result = product.Validate();

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "TDD 2 Green: precio en limite cero es invalido")]
    public void ProductWithZeroPriceIsInvalid()
    {
        // Arrange
        var product = ProductTestData.Product()
            .WithPrice(0m)
            .Build();

        // Act
        var result = product.Validate();

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "TDD 3 Refactor: errores se acumulan sin perder legibilidad")]
    public void ProductValidationAccumulatesErrors()
    {
        // Arrange
        var product = ProductTestData.Product()
            .WithId(Guid.Empty)
            .WithName(string.Empty)
            .WithCategory(string.Empty)
            .WithPrice(-10m)
            .Build();

        // Act
        var result = product.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(4, result.Errors.Count);
    }
}
