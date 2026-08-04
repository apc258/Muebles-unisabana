using OrderService.Api.Validators;
using Xunit;

namespace OrderService.Tests;

public sealed class OrderRequestValidatorTests
{
    private static readonly Guid ValidCustomerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ValidProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact(DisplayName = "Unitaria: orden con customerId y un item valido es valida")]
    public void Validate_WithValidPayload_ReturnsValid()
    {
        var items = new[] { (ValidProductId, 1, 100m) };
        var result = OrderRequestValidator.Validate(ValidCustomerId, items);
        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "Unitaria: customerId vacio retorna invalido")]
    public void Validate_WithEmptyCustomerId_ReturnsInvalid()
    {
        var items = new[] { (ValidProductId, 1, 100m) };
        var result = OrderRequestValidator.Validate(Guid.Empty, items);
        Assert.False(result.IsValid);
        Assert.Equal("customerId y al menos un item son obligatorios", result.ErrorMessage);
    }

    [Fact(DisplayName = "Unitaria: lista de items vacia retorna invalido")]
    public void Validate_WithEmptyItems_ReturnsInvalid()
    {
        var items = Array.Empty<(Guid, int, decimal)>();
        var result = OrderRequestValidator.Validate(ValidCustomerId, items);
        Assert.False(result.IsValid);
        Assert.Equal("customerId y al menos un item son obligatorios", result.ErrorMessage);
    }

    [Fact(DisplayName = "Unitaria: items null retorna invalido")]
    public void Validate_WithNullItems_ReturnsInvalid()
    {
        var result = OrderRequestValidator.Validate(ValidCustomerId, null);
        Assert.False(result.IsValid);
    }

    [Theory(DisplayName = "Unitaria: item con productId vacio, quantity<=0 o unitPrice<=0 retorna invalido")]
    [InlineData(0, 1, 100, "productId vacio")]
    [InlineData(1, 0, 100, "quantity cero")]
    [InlineData(1, -1, 100, "quantity negativa")]
    [InlineData(1, 1, 0, "unitPrice cero")]
    [InlineData(1, 1, -1, "unitPrice negativo")]
    public void Validate_WithInvalidItem_ReturnsInvalid(int productMarker, int quantity, decimal unitPrice, string scenario)
    {
        var pid = productMarker == 0 ? Guid.Empty : ValidProductId;
        var items = new[] { (pid, quantity, unitPrice) };
        var result = OrderRequestValidator.Validate(ValidCustomerId, items);
        Assert.False(result.IsValid);
        Assert.Equal("Todos los items deben tener productId, quantity y unitPrice válidos", result.ErrorMessage);
    }
}
