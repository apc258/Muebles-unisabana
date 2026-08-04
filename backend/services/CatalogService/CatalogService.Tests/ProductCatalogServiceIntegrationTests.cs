using CatalogService.Application;
using CatalogService.Infrastructure;
using Xunit;

namespace CatalogService.Tests;

public sealed class ProductCatalogServiceIntegrationTests
{
    [Fact(DisplayName = "Integral: catalogo filtra precios invalidos y ordena productos por nombre")]
    public void GetAvailableProducts_WhenRepositoryHasInvalidPrices_ReturnsOnlyPositivePricesOrderedByName()
    {
        // Arrange
        var executionId = Guid.NewGuid().ToString("N");
        var mainProduct = ProductTestData.CatalogProduct()
            .WithName($"A producto principal {executionId}")
            .WithPrice(2499m)
            .Build();
        var secondaryProduct = ProductTestData.CatalogProduct()
            .WithName($"B producto secundario {executionId}")
            .WithCategory("Entrada")
            .WithPrice(850m)
            .Build();
        var unavailableProduct = ProductTestData.CatalogProduct()
            .WithName($"C producto sin precio valido {executionId}")
            .WithCategory("Comedor")
            .WithPrice(0m)
            .Build();
        var repository = new InMemoryProductRepository(new[]
        {
            secondaryProduct,
            mainProduct,
            unavailableProduct
        });
        var service = new ProductCatalogService(repository);

        // Act
        var products = service.GetAvailableProducts();

        // Assert
        Assert.Collection(
            products,
            first => Assert.Equal(mainProduct.Name, first.Name),
            second => Assert.Equal(secondaryProduct.Name, second.Name));
    }
}
