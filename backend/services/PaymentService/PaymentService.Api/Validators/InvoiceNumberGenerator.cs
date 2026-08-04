namespace PaymentService.Api.Validators;

internal static class InvoiceNumberGenerator
{
    public static string Generate(DateTime issuedAt, Guid paymentId)
    {
        return $"FAC-{issuedAt:yyyyMMdd}-{paymentId.ToString()[..8].ToUpperInvariant()}";
    }
}
