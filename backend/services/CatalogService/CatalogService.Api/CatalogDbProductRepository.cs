using CatalogService.Application;
using Npgsql;

namespace CatalogService.Api;

public sealed class CatalogDbProductRepository : IProductRepository
{
    private readonly string _connectionString;

    public CatalogDbProductRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IReadOnlyList<CatalogProductDto> GetProducts()
    {
        var products = new List<CatalogProductDto>();
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT product_id, name, category, price, image, colors, measures
            FROM inventory_products
            ORDER BY created_at DESC;
        ";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            products.Add(new CatalogProductDto(
                reader.GetGuid(0).ToString(),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetString(4),
                SplitList(reader.GetString(5)),
                SplitList(reader.GetString(6))));
        }

        return products;
    }

    private static List<string> SplitList(string raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
