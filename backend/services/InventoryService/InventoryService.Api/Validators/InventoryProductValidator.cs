namespace InventoryService.Api.Validators;

internal static class InventoryProductValidator
{
    public static InventoryValidationResult Validate(string? sku, string? name, string? category, decimal price)
    {
        if (string.IsNullOrWhiteSpace(sku) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(category) ||
            price <= 0)
        {
            return InventoryValidationResult.Invalid("sku, name, category y price son obligatorios");
        }

        return InventoryValidationResult.Valid();
    }

    public static List<string> SplitList(string raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}

internal sealed record InventoryValidationResult(bool IsValid, string? ErrorMessage)
{
    public static InventoryValidationResult Valid() => new(true, null);
    public static InventoryValidationResult Invalid(string message) => new(false, message);
}
