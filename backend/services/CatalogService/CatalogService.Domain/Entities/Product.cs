namespace CatalogService.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public ProductValidationResult Validate()
    {
        var errors = new List<string>();

        if (Id == Guid.Empty)
        {
            errors.Add("El identificador del producto es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("El nombre del producto es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(Category))
        {
            errors.Add("La categoria del producto es obligatoria.");
        }

        if (Price <= 0)
        {
            errors.Add("El precio debe ser mayor que cero.");
        }

        return new ProductValidationResult(errors.Count == 0, errors);
    }
}

public sealed record ProductValidationResult(bool IsValid, IReadOnlyList<string> Errors);
