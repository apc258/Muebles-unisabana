using AuthService.Api.Validators;
using Xunit;

namespace AuthService.Tests;

public sealed class AuthRequestValidatorTests
{
    [Fact(DisplayName = "Unitaria: registro con todos los campos es valido")]
    public void ValidateRegister_WhenAllFieldsPresent_ReturnsValid()
    {
        // Act
        var result = AuthRequestValidator.ValidateRegister(
            email: "cliente@muebles.com",
            password: "Password123!",
            fullName: "Cliente Demo",
            identification: "1234567890");

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Theory(DisplayName = "Unitaria: registro con campos faltantes retorna error")]
    [InlineData(null, "Password123!", "Cliente", "1234567890")]
    [InlineData("", "Password123!", "Cliente", "1234567890")]
    [InlineData("cliente@muebles.com", null, "Cliente", "1234567890")]
    [InlineData("cliente@muebles.com", "Password123!", "", "1234567890")]
    [InlineData("cliente@muebles.com", "Password123!", "Cliente", " ")]
    public void ValidateRegister_WhenFieldsMissing_ReturnsInvalid(string? email, string? password, string? fullName, string? identification)
    {
        // Act
        var result = AuthRequestValidator.ValidateRegister(email, password, fullName, identification);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Email, password, fullName e identification son obligatorios", result.ErrorMessage);
    }

    [Fact(DisplayName = "Unitaria: login con email y password validos retorna valido")]
    public void ValidateLogin_WhenValid_ReturnsValid()
    {
        var result = AuthRequestValidator.ValidateLogin("admin@muebles.com", "Admin123*");
        Assert.True(result.IsValid);
    }

    [Theory(DisplayName = "Unitaria: login con campos vacios retorna invalido")]
    [InlineData("", "Admin123*")]
    [InlineData("admin@muebles.com", "")]
    [InlineData(null, null)]
    public void ValidateLogin_WhenFieldsMissing_ReturnsInvalid(string? email, string? password)
    {
        var result = AuthRequestValidator.ValidateLogin(email, password);
        Assert.False(result.IsValid);
        Assert.Equal("Email y password son obligatorios", result.ErrorMessage);
    }

    [Theory(DisplayName = "Unitaria: normalizacion de email convierte a minusculas y recorta")]
    [InlineData("  Admin@Muebles.com  ", "admin@muebles.com")]
    [InlineData("USER@EXAMPLE.com", "user@example.com")]
    public void NormalizeEmail_AlwaysReturnsTrimmedLowercase(string raw, string expected)
    {
        Assert.Equal(expected, AuthRequestValidator.NormalizeEmail(raw));
    }

    [Fact(DisplayName = "Unitaria: rol por defecto es Customer cuando no es admin")]
    public void ResolveRole_WhenNotAdmin_ReturnsCustomer()
    {
        var role = AuthRequestValidator.ResolveRole(isAdmin: false, requestedRole: "Admin");
        Assert.Equal("Customer", role);
    }

    [Fact(DisplayName = "Unitaria: rol Admin se respeta cuando el solicitante es admin")]
    public void ResolveRole_WhenAdminAndRequestedRoleProvided_ReturnsRequestedRole()
    {
        var role = AuthRequestValidator.ResolveRole(isAdmin: true, requestedRole: "Admin");
        Assert.Equal("Admin", role);
    }

    [Fact(DisplayName = "Unitaria: cuando es admin pero no envia rol, se asigna Customer")]
    public void ResolveRole_WhenAdminButNoRoleRequested_ReturnsCustomer()
    {
        var role = AuthRequestValidator.ResolveRole(isAdmin: true, requestedRole: null);
        Assert.Equal("Customer", role);
    }
}
