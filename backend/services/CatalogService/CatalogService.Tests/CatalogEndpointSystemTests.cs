using CatalogService.Domain.Entities;
using Xunit;

namespace CatalogService.Tests;

public sealed class CatalogEndpointSystemTests
{
    [Fact(DisplayName = "Sistema: POST de validacion con producto valido equivale a HTTP 200")]
    public void ShouldReturnValidWhenPostRequest()
    {
        // Arrange
        var request = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Biblioteca Modular",
            Category = "Estudio",
            Price = 1800m
        };

        // Act
        var result = request.Validate();
        var httpStatus = result.IsValid ? 200 : 400;

        // Assert
        Assert.Equal(200, httpStatus);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "Sistema: POST de validacion con precio cero equivale a HTTP 400")]
    public void ShouldReturnBadRequestWhenPostRequestHasBoundaryPriceZero()
    {
        // Arrange
        var request = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Biblioteca Modular",
            Category = "Estudio",
            Price = 0m
        };

        // Act
        var result = request.Validate();
        var httpStatus = result.IsValid ? 200 : 400;

        // Assert
        Assert.Equal(400, httpStatus);
        Assert.False(result.IsValid);
        Assert.Contains("El precio debe ser mayor que cero.", result.Errors);
    }
}
