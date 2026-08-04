using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuthService.Tests;

// Equivalente al RegistryControllerIT del Word: arranca el servidor en memoria, hace peticiones HTTP reales
// y verifica las respuestas. Usa una base SQLite temporal para no tocar la BD real.
public sealed class AuthEndpointsSystemTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public AuthEndpointsSystemTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    // --- PRUEBA HTTP 1: login con admin sembrado retorna 200 y token ---
    [Fact(DisplayName = "Sistema: POST /api/auth/login con admin sembrado retorna 200 y token")]
    public async Task Login_WithSeededAdmin_ReturnsOkWithToken()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new { email = "admin@muebles.com", password = "Admin123*" };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", payload);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("token", out var tokenElement));
        Assert.False(string.IsNullOrWhiteSpace(tokenElement.GetString()));
        Assert.Equal("admin@muebles.com", root.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal("Admin", root.GetProperty("user").GetProperty("role").GetString());
    }

    // --- PRUEBA HTTP 2: login con password incorrecto retorna 401 ---
    [Fact(DisplayName = "Sistema: POST /api/auth/login con password invalido retorna 401")]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new { email = "admin@muebles.com", password = "claveIncorrecta" };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", payload);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- PRUEBA HTTP 3: login sin email retorna 400 ---
    [Fact(DisplayName = "Sistema: POST /api/auth/login sin email retorna 400")]
    public async Task Login_WithoutEmail_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var payload = new { email = "", password = "Admin123*" };

        var response = await client.PostAsJsonAsync("/api/auth/login", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- PRUEBA HTTP 4: registro de cliente nuevo retorna 201 ---
    [Fact(DisplayName = "Sistema: POST /api/auth/register con datos validos retorna 201")]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        var client = _factory.CreateClient();
        var email = $"cliente-{Guid.NewGuid():N}@muebles.com";
        var payload = new
        {
            email,
            fullName = "Cliente Nuevo",
            identification = "1122334455",
            password = "Pass123*"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(email, doc.RootElement.GetProperty("email").GetString());
        Assert.Equal("Customer", doc.RootElement.GetProperty("role").GetString());
    }

    // --- PRUEBA HTTP 5: registro duplicado retorna 409 ---
    [Fact(DisplayName = "Sistema: POST /api/auth/register con email existente retorna 409")]
    public async Task Register_WithExistingEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@muebles.com";
        var payload = new
        {
            email,
            fullName = "Duplicado",
            identification = "9999999999",
            password = "Pass123*"
        };

        // primera vez OK
        var first = await client.PostAsJsonAsync("/api/auth/register", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // segunda vez Conflict
        var second = await client.PostAsJsonAsync("/api/auth/register", payload);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // --- PRUEBA HTTP 6: registro sin campos requeridos retorna 400 ---
    [Fact(DisplayName = "Sistema: POST /api/auth/register sin password retorna 400")]
    public async Task Register_WithoutPassword_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var payload = new
        {
            email = "x@y.com",
            fullName = "X",
            identification = "1",
            password = ""
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- PRUEBA HTTP 7: forgot-password con email no registrado retorna 200 (silent) ---
    [Fact(DisplayName = "Sistema: POST /api/auth/forgot-password con email desconocido retorna 200")]
    public async Task ForgotPassword_WithUnknownEmail_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var payload = new { email = $"unknown-{Guid.NewGuid():N}@muebles.com", fullName = (string?)null };

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- PRUEBA HTTP 8: forgot-password sin email retorna 400 ---
    [Fact(DisplayName = "Sistema: POST /api/auth/forgot-password sin email retorna 400")]
    public async Task ForgotPassword_WithoutEmail_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var payload = new { email = "", fullName = "X" };

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- PRUEBA HTTP 9: GET /api/auth/users sin Admin retorna 200 con lista vacia ---
    [Fact(DisplayName = "Sistema: GET /api/auth/users sin Admin retorna 200 con lista vacia")]
    public async Task GetUsers_WithoutAdmin_ReturnsEmptyList()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    // --- PRUEBA HTTP 10: GET /api/auth/users como Admin retorna usuarios sembrados ---
    [Fact(DisplayName = "Sistema: GET /api/auth/users con header Admin retorna usuarios")]
    public async Task GetUsers_AsAdmin_ReturnsUsers()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Role", "Admin");

        var response = await client.GetAsync("/api/auth/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetArrayLength() >= 1); // al menos el admin sembrado
    }

    // --- PRUEBA HTTP 11: PUT user sin Admin retorna 403 ---
    [Fact(DisplayName = "Sistema: PUT /api/auth/users/{id} sin Admin retorna 403")]
    public async Task UpdateUser_WithoutAdmin_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        var payload = new { email = "x@y.com", fullName = "X", identification = "1" };

        var response = await client.PutAsJsonAsync($"/api/auth/users/{Guid.NewGuid()}", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- PRUEBA HTTP 12: PUT user como Admin con id desconocido retorna 404 ---
    [Fact(DisplayName = "Sistema: PUT /api/auth/users/{id} como Admin con id desconocido retorna 404")]
    public async Task UpdateUser_AsAdminUnknownId_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Role", "Admin");
        var payload = new { email = "x@y.com", fullName = "X", identification = "1" };

        var response = await client.PutAsJsonAsync($"/api/auth/users/{Guid.NewGuid()}", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- PRUEBA HTTP 13: ciclo completo register -> get -> update -> delete (Admin) ---
    [Fact(DisplayName = "Sistema: ciclo completo Admin - register, list, update y delete usuario")]
    public async Task FullCycle_RegisterListUpdateDelete_AsAdmin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Role", "Admin");

        // 1. Register
        var email = $"cycle-{Guid.NewGuid():N}@muebles.com";
        var registerResp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            fullName = "Cycle User",
            identification = "9876543210",
            password = "Pass123*"
        });
        Assert.Equal(HttpStatusCode.Created, registerResp.StatusCode);
        var registerBody = await registerResp.Content.ReadAsStringAsync();
        var userId = JsonDocument.Parse(registerBody).RootElement.GetProperty("id").GetGuid();

        // 2. Update
        var updateResp = await client.PutAsJsonAsync($"/api/auth/users/{userId}", new
        {
            email = $"updated-{Guid.NewGuid():N}@muebles.com",
            fullName = "Cycle Renamed",
            identification = "9876543210",
            password = (string?)null,
            role = "Admin",
            isActive = (bool?)false
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updateBody = await updateResp.Content.ReadAsStringAsync();
        var updateDoc = JsonDocument.Parse(updateBody).RootElement;
        Assert.Equal("Cycle Renamed", updateDoc.GetProperty("fullName").GetString());
        Assert.Equal("Admin", updateDoc.GetProperty("role").GetString());

        // 3. Delete
        var deleteResp = await client.DeleteAsync($"/api/auth/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);
    }

    // --- PRUEBA HTTP 14: DELETE user sin Admin retorna 403 ---
    [Fact(DisplayName = "Sistema: DELETE /api/auth/users/{id} sin Admin retorna 403")]
    public async Task DeleteUser_WithoutAdmin_ReturnsForbidden()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/auth/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- PRUEBA HTTP 15: DELETE como Admin con id desconocido retorna 404 ---
    [Fact(DisplayName = "Sistema: DELETE /api/auth/users/{id} como Admin con id desconocido retorna 404")]
    public async Task DeleteUser_AsAdminUnknownId_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Role", "Admin");

        var response = await client.DeleteAsync($"/api/auth/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- PRUEBA HTTP 16: PUT user con email duplicado retorna 409 ---
    [Fact(DisplayName = "Sistema: PUT /api/auth/users/{id} con email duplicado retorna 409")]
    public async Task UpdateUser_WithDuplicateEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Role", "Admin");

        // Creamos dos usuarios
        var emailA = $"a-{Guid.NewGuid():N}@muebles.com";
        var emailB = $"b-{Guid.NewGuid():N}@muebles.com";

        var respA = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = emailA, fullName = "A", identification = "1", password = "Pass123*"
        });
        var respB = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = emailB, fullName = "B", identification = "2", password = "Pass123*"
        });
        var bodyB = await respB.Content.ReadAsStringAsync();
        var idB = JsonDocument.Parse(bodyB).RootElement.GetProperty("id").GetGuid();

        // Intentamos cambiar B.email = A.email -> Conflict
        var putResp = await client.PutAsJsonAsync($"/api/auth/users/{idB}", new
        {
            email = emailA, fullName = "B", identification = "2"
        });

        Assert.Equal(HttpStatusCode.Conflict, putResp.StatusCode);
    }
}

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"auth-system-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:AuthDb", $"Data Source={_dbPath};Pooling=False");
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch
            {
                // archivo bloqueado en CI, ignorar
            }
        }
    }
}
