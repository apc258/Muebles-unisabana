namespace PaymentService.Api.Validators;

internal static class PaymentTotalsCalculator
{
    public const decimal IvaRate = 0.16m;

    public static PaymentTotals Calculate(IEnumerable<(int Quantity, decimal UnitPrice)> items)
    {
        var subtotal = items.Sum(item => item.Quantity * item.UnitPrice);
        var tax = Math.Round(subtotal * IvaRate, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + tax;
        return new PaymentTotals(subtotal, tax, total);
    }
}

internal sealed record PaymentTotals(decimal Subtotal, decimal Tax, decimal Total);
