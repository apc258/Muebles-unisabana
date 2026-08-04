using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AuthService.Tests;

public sealed class PasswordRecoveryNotifierMockTests
{
    // --- PRUEBA MOCK 1: cuando no hay SMTP configurado, el notificador no lanza error y consulta claves esperadas ---
    [Fact(DisplayName = "Mock: sin SMTP configurado, NotifyAdminAsync no lanza y consulta ADMIN_EMAIL")]
    public async Task NotifyAdminAsync_WhenSmtpHostMissing_DoesNotThrowAndQueriesAdminEmail()
    {
        // Arrange: mockeamos IConfiguration para que devuelva null en todas las claves
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c[It.IsAny<string>()]).Returns((string?)null);

        var notifier = new PasswordRecoveryNotifier(configMock.Object);
        var user = new UserRecord(
            Guid.NewGuid(),
            "cliente@muebles.com",
            "Cliente Demo",
            "123",
            "hash",
            "Customer",
            DateTime.UtcNow,
            true);

        // Act
        var act = async () => await notifier.NotifyAdminAsync(user, "Cliente Indicado");

        // Assert
        await act.Invoke();
        configMock.Verify(c => c[It.Is<string>(s => s == "ADMIN_EMAIL" || s == "AdminEmail")], Times.AtLeastOnce);
        configMock.Verify(c => c[It.Is<string>(s => s == "SMTP_HOST" || s == "Smtp:Host")], Times.AtLeastOnce);
    }

    // --- PRUEBA MOCK 2: ADMIN_EMAIL customizado se lee de configuracion (interaccion verificable) ---
    [Fact(DisplayName = "Mock: ADMIN_EMAIL custom es leido de IConfiguration cuando esta configurado")]
    public async Task NotifyAdminAsync_WhenAdminEmailConfigured_ReadsItFromConfiguration()
    {
        // Arrange
        const string customAdminEmail = "supervisor@muebles.com";
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ADMIN_EMAIL"]).Returns(customAdminEmail);
        configMock.Setup(c => c["AdminEmail"]).Returns((string?)null);
        configMock.Setup(c => c[It.Is<string>(s => s != "ADMIN_EMAIL" && s != "AdminEmail")]).Returns((string?)null);

        var notifier = new PasswordRecoveryNotifier(configMock.Object);
        var user = new UserRecord(
            Guid.NewGuid(),
            "cliente@muebles.com",
            "Cliente Demo",
            "123",
            "hash",
            "Customer",
            DateTime.UtcNow,
            true);

        // Capturamos la salida de consola para confirmar que el email custom se usa como destinatario
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Act
            await notifier.NotifyAdminAsync(user, requestedName: null);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert: interaccion - se consulto ADMIN_EMAIL exactamente una vez (al menos)
        configMock.Verify(c => c["ADMIN_EMAIL"], Times.AtLeastOnce);
        // Y se uso el valor configurado en el log
        Assert.Contains(customAdminEmail, stringWriter.ToString());
    }
}
