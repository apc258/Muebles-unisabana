namespace OrderService.Api.Validators;

internal static class OrderRequestValidator
{
    public static OrderValidationResult Validate(Guid customerId, IReadOnlyList<(Guid ProductId, int Quantity, decimal UnitPrice)>? items)
    {
        if (customerId == Guid.Empty || items is null || items.Count == 0)
        {
            return OrderValidationResult.Invalid("customerId y al menos un item son obligatorios");
        }

        if (items.Any(item => item.ProductId == Guid.Empty || item.Quantity <= 0 || item.UnitPrice <= 0))
        {
            return OrderValidationResult.Invalid("Todos los items deben tener productId, quantity y unitPrice válidos");
        }

        return OrderValidationResult.Valid();
    }
}

internal sealed record OrderValidationResult(bool IsValid, string? ErrorMessage)
{
    public static OrderValidationResult Valid() => new(true, null);
    public static OrderValidationResult Invalid(string message) => new(false, message);
}
