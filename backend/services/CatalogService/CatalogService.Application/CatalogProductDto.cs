namespace CatalogService.Application;

public sealed record CatalogProductDto(
    string Id,
    string Name,
    string Category,
    decimal Price,
    string Image,
    IReadOnlyList<string> Colors,
    IReadOnlyList<string> Measures);
