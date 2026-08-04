using CatalogService.Application;
using CatalogService.Domain.Entities;

namespace CatalogService.Tests;

internal static class ProductTestData
{
    public static ProductBuilder Product() => new();

    public static CatalogProductBuilder CatalogProduct() => new();
}

internal sealed class ProductBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = $"Producto prueba {Guid.NewGuid():N}";
    private string _category = "Sala";
    private decimal _price = 244m;

    public ProductBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ProductBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ProductBuilder WithCategory(string category)
    {
        _category = category;
        return this;
    }

    public ProductBuilder WithPrice(decimal price)
    {
        _price = price;
        return this;
    }

    public Product Build()
    {
        return new Product
        {
            Id = _id,
            Name = _name,
            Category = _category,
            Price = _price
        };
    }
}

internal sealed class CatalogProductBuilder
{
    private string _id = $"prod-{Guid.NewGuid():N}";
    private string _name = $"Producto catalogo {Guid.NewGuid():N}";
    private string _category = "Sala";
    private decimal _price = 2499m;
    private string _image = $"producto-{Guid.NewGuid():N}.jpg";
    private string[] _colors = ["Gris"];
    private string[] _measures = ["200x90"];

    public CatalogProductBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public CatalogProductBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CatalogProductBuilder WithCategory(string category)
    {
        _category = category;
        return this;
    }

    public CatalogProductBuilder WithPrice(decimal price)
    {
        _price = price;
        return this;
    }

    public CatalogProductDto Build()
    {
        return new CatalogProductDto(_id, _name, _category, _price, _image, _colors, _measures);
    }
}
