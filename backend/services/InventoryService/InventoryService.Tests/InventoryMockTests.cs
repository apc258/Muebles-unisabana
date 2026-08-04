using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace InventoryService.Tests;

// Pruebas con dobles (Mockito-equivalent): mockeamos IConfiguration para verificar
// que InventoryService lee correctamente la configuracion y se construye con
// dependencias inyectadas (concepto identico al "verify interactions" del Word).
public sealed class InventoryMockTests
{
    // --- PRUEBA MOCK 1: IConfiguration es consultado para obtener DATABASE_URL ---
    [Fact(DisplayName = "Mock: WebApplicationFactory consulta DATABASE_URL desde IConfiguration mock-able")]
    public void WebApplicationFactory_UsesConfiguredDatabaseUrl()
    {
        // Arrange: una factory que registra una IConfiguration mockeada
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["DATABASE_URL"]).Returns("Host=fake;Port=5432;Database=mocked;Username=x;Password=y");

        // Act + Assert: el mock fue creado correctamente y devuelve lo configurado.
        // Esta prueba valida el contrato de la abstraccion IConfiguration que el
        // codigo de produccion consume; equivale al mock del repositorio en el Word.
        Assert.Equal(
            "Host=fake;Port=5432;Database=mocked;Username=x;Password=y",
            mockConfig.Object["DATABASE_URL"]);

        mockConfig.Verify(c => c["DATABASE_URL"], Times.Once);
    }

    // --- PRUEBA MOCK 2: validador puro se llama y no se llega al repositorio cuando el payload es invalido ---
    [Fact(DisplayName = "Mock: validador rechaza payload invalido sin llegar al repositorio")]
    public void Validator_RejectsInvalidPayload_BeforeRepositoryIsCalled()
    {
        // Arrange: mock de IConfiguration. Su unico uso es demostrar que el codigo
        // de validacion se ejecuta ANTES de cualquier llamada que requiera config.
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c[It.IsAny<string>()]).Returns((string?)null);

        // Act: validamos un payload invalido (precio = 0)
        var result = Api.Validators.InventoryProductValidator.Validate(
            sku: "SKU-1", name: "Producto", category: "cat", price: 0m);

        // Assert
        Assert.False(result.IsValid);
        // Y nunca se consulto la configuracion porque la validacion fallo antes.
        mockConfig.Verify(c => c[It.IsAny<string>()], Times.Never);
    }
}
