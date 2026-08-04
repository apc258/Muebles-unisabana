using CatalogService.Application;

namespace CatalogService.Infrastructure;

public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly IReadOnlyList<CatalogProductDto> _products;

    public InMemoryProductRepository(IEnumerable<CatalogProductDto> products)
    {
        _products = products.ToList();
    }

    public IReadOnlyList<CatalogProductDto> GetProducts()
    {
        return _products;
    }
}
