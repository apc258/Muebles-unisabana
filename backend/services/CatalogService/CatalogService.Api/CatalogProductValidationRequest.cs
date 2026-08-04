using CatalogService.Domain.Entities;

namespace CatalogService.Api;

public sealed record CatalogProductValidationRequest(Guid Id, string Name, string Category, decimal Price)
{
    public Product ToProduct()
    {
        return new Product
        {
            Id = Id,
            Name = Name,
            Category = Category,
            Price = Price
        };
    }
}
