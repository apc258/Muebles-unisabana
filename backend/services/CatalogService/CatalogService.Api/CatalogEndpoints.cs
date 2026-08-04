using CatalogService.Application;

namespace CatalogService.Api;

public static class CatalogEndpoints
{
    public static IResult GetCatalog(ProductCatalogService service)
    {
        return Results.Ok(service.GetAvailableProducts());
    }

    public static IResult ValidateProduct(CatalogProductValidationRequest request)
    {
        var result = request.ToProduct().Validate();
        return result.IsValid ? Results.Ok(result) : Results.BadRequest(result);
    }
}
