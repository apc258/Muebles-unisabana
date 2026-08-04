namespace PaymentService.Api.Validators;

internal static class PaymentRequestValidator
{
    public static PaymentValidationResult Validate(Guid orderId, string? customerName, string? customerEmail, int itemsCount)
    {
        if (orderId == Guid.Empty ||
            string.IsNullOrWhiteSpace(customerName) ||
            string.IsNullOrWhiteSpace(customerEmail) ||
            itemsCount == 0)
        {
            return PaymentValidationResult.Invalid("orderId, customerName, customerEmail e items son obligatorios");
        }

        return PaymentValidationResult.Valid();
    }
}

internal sealed record PaymentValidationResult(bool IsValid, string? ErrorMessage)
{
    public static PaymentValidationResult Valid() => new(true, null);
    public static PaymentValidationResult Invalid(string message) => new(false, message);
}
