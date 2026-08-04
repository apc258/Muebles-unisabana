namespace CartService.Api.Validators;

internal static class CartItemValidator
{
    public static CartValidationResult Validate(int quantity, decimal unitPrice)
    {
        if (quantity <= 0 || unitPrice < 0)
        {
            return CartValidationResult.Invalid("Cantidad o precio inválidos.");
        }

        return CartValidationResult.Valid();
    }

    // Calcula totales del carrito: subtotal, IVA 16%, total
    public static CartTotals CalculateTotals(IEnumerable<(int Quantity, decimal UnitPrice)> items)
    {
        var subtotal = items.Sum(item => item.Quantity * item.UnitPrice);
        var tax = Math.Round(subtotal * 0.16m, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + tax;
        return new CartTotals(subtotal, tax, total);
    }
}

internal sealed record CartValidationResult(bool IsValid, string? ErrorMessage)
{
    public static CartValidationResult Valid() => new(true, null);
    public static CartValidationResult Invalid(string message) => new(false, message);
}

internal sealed record CartTotals(decimal Subtotal, decimal Tax, decimal Total);
