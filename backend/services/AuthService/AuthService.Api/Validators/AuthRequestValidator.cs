namespace AuthService.Api.Validators;

internal static class AuthRequestValidator
{
    public static AuthValidationResult ValidateRegister(string? email, string? password, string? fullName, string? identification)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(identification))
        {
            return AuthValidationResult.Invalid("Email, password, fullName e identification son obligatorios");
        }

        return AuthValidationResult.Valid();
    }

    public static AuthValidationResult ValidateLogin(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return AuthValidationResult.Invalid("Email y password son obligatorios");
        }

        return AuthValidationResult.Valid();
    }

    public static AuthValidationResult ValidateUpdate(string? email, string? fullName, string? identification)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(identification))
        {
            return AuthValidationResult.Invalid("Email, fullName e identification son obligatorios");
        }

        return AuthValidationResult.Valid();
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string ResolveRole(bool isAdmin, string? requestedRole)
    {
        if (isAdmin && !string.IsNullOrWhiteSpace(requestedRole))
        {
            return requestedRole.Trim();
        }

        return "Customer";
    }
}

internal sealed record AuthValidationResult(bool IsValid, string? ErrorMessage)
{
    public static AuthValidationResult Valid() => new(true, null);
    public static AuthValidationResult Invalid(string message) => new(false, message);
}
