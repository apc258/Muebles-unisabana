namespace CatalogService.Application;

public sealed class ProductCatalogService
{
    private readonly IProductRepository _repository;

    public ProductCatalogService(IProductRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<CatalogProductDto> GetAvailableProducts()
    {
        return _repository.GetProducts()
            .Where(product => product.Price > 0)
            .OrderBy(product => product.Name)
            .ToList();
    }
}
