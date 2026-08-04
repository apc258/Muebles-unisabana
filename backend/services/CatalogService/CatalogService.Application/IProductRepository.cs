namespace CatalogService.Application;

public interface IProductRepository
{
    IReadOnlyList<CatalogProductDto> GetProducts();
}
