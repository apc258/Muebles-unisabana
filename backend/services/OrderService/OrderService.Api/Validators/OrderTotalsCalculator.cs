namespace OrderService.Api.Validators;

internal static class OrderTotalsCalculator
{
    public const decimal IvaRate = 0.16m;

    public static OrderTotals Calculate(IEnumerable<(int Quantity, decimal UnitPrice)> items)
    {
        var subtotal = items.Sum(item => item.Quantity * item.UnitPrice);
        var tax = Math.Round(subtotal * IvaRate, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + tax;
        return new OrderTotals(subtotal, tax, total);
    }
}

internal sealed record OrderTotals(decimal Subtotal, decimal Tax, decimal Total);
