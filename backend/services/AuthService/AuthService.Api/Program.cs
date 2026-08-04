using System.Data;
using System.Net;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using BCrypt.Net;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("AuthService.Tests")]

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("AuthDb") ?? "Data Source=auth.db";

builder.Services.AddSingleton(new AuthDb(connectionString));
builder.Services.AddSingleton<PasswordRecoveryNotifier>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDb>();
    db.Initialize();
}

app.MapGet("/health", () => Results.Ok(new { service = "AuthService" }));

app.MapPost("/api/auth/register", (HttpContext httpContext, RegisterRequest request, AuthDb db) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Identification))
    {
        return Results.BadRequest(new { message = "Email, password, fullName e identification son obligatorios" });
    }

    var normalizedEmail = request.Email.Trim().ToLowerInvariant();
    var existingUser = db.GetUserByEmail(normalizedEmail);
    if (existingUser is not null)
    {
        return Results.Conflict(new { message = "El usuario ya existe" });
    }

    var role = IsAdmin(httpContext) && !string.IsNullOrWhiteSpace(request.Role)
        ? request.Role.Trim()
        : "Customer";

    var user = new UserRecord(
        Guid.NewGuid(),
        normalizedEmail,
        request.FullName.Trim(),
        request.Identification.Trim(),
        BCrypt.Net.BCrypt.HashPassword(request.Password),
        role,
        DateTime.UtcNow,
        true);

    db.CreateUser(user);

    return Results.Created($"/api/auth/users/{user.Id}", ToUserResponse(user));
});

app.MapPost("/api/auth/forgot-password", async (ForgotPasswordRequest request, AuthDb db, PasswordRecoveryNotifier notifier) =>
{
    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new { message = "Email es obligatorio" });
    }

    var normalizedEmail = request.Email.Trim().ToLowerInvariant();
    var user = db.GetUserByEmail(normalizedEmail);

    if (user is not null)
    {
        await notifier.NotifyAdminAsync(user, request.FullName);
    }

    return Results.Ok(new { message = "Su operacion esta en progreso" });
});

app.MapPost("/api/auth/login", (LoginRequest request, AuthDb db) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Email y password son obligatorios" });
    }

    var user = db.GetUserByEmail(request.Email.Trim().ToLowerInvariant());
    if (user is null || !user.IsActive)
    {
        return Results.Unauthorized();
    }

    var isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
    if (!isValid)
    {
        return Results.Unauthorized();
    }

    var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    return Results.Ok(new
    {
        token,
        expiresIn = 3600,
        user = new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.Identification,
            user.Role
        }
    });
});

// --- CAMBIO APLICADO AQUÍ ---
app.MapGet("/api/auth/users", (HttpContext httpContext, AuthDb db) =>
{
    // Si no es admin, devolvemos lista vacía en vez de 403
    if (!IsAdmin(httpContext))
    {
        return Results.Ok(new List<object>());
    }

    var users = db.GetUsers()
        .Select(ToUserResponse);

    return Results.Ok(users);
});
// ----------------------------

app.MapPut("/api/auth/users/{id:guid}", (HttpContext httpContext, Guid id, UpdateUserRequest request, AuthDb db) =>
{
    var adminGuard = RequireAdmin(httpContext);
    if (adminGuard is not null)
    {
        return adminGuard;
    }

    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Identification))
    {
        return Results.BadRequest(new { message = "Email, fullName e identification son obligatorios" });
    }

    var existingUser = db.GetUserById(id);
    if (existingUser is null)
    {
        return Results.NotFound(new { message = "Usuario no encontrado" });
    }

    var normalizedEmail = request.Email.Trim().ToLowerInvariant();
    var userByEmail = db.GetUserByEmail(normalizedEmail);
    if (userByEmail is not null && userByEmail.Id != id)
    {
        return Results.Conflict(new { message = "Ya existe otro usuario con ese email" });
    }

    var updatedUser = existingUser with
    {
        Email = normalizedEmail,
        FullName = request.FullName.Trim(),
        Identification = request.Identification.Trim(),
        PasswordHash = string.IsNullOrWhiteSpace(request.Password)
            ? existingUser.PasswordHash
            : BCrypt.Net.BCrypt.HashPassword(request.Password),
        Role = string.IsNullOrWhiteSpace(request.Role)
            ? existingUser.Role
            : request.Role.Trim(),
        IsActive = request.IsActive ?? existingUser.IsActive
    };

    db.UpdateUser(updatedUser);

    return Results.Ok(ToUserResponse(updatedUser));
});

app.MapDelete("/api/auth/users/{id:guid}", (HttpContext httpContext, Guid id, AuthDb db) =>
{
    var adminGuard = RequireAdmin(httpContext);
    if (adminGuard is not null)
    {
        return adminGuard;
    }

    var existingUser = db.GetUserById(id);
    if (existingUser is null)
    {
        return Results.NotFound(new { message = "Usuario no encontrado" });
    }

    db.DeleteUser(id);
    return Results.Ok(new { message = "Usuario eliminado" });
});

app.Run();

static object ToUserResponse(UserRecord user) => new
{
    user.Id,
    user.Email,
    user.FullName,
    user.Identification,
    user.Role,
    user.CreatedAt,
    user.IsActive
};

static Guid? GetCurrentUserId(HttpContext httpContext)
{
    var raw = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
    return Guid.TryParse(raw, out var id) ? id : null;
}

static string GetCurrentUserRole(HttpContext httpContext)
{
    return httpContext.Request.Headers["X-User-Role"].FirstOrDefault() ?? string.Empty;
}

static bool IsAdmin(HttpContext httpContext)
{
    return string.Equals(GetCurrentUserRole(httpContext), "Admin", StringComparison.OrdinalIgnoreCase);
}

static IResult? RequireAdmin(HttpContext httpContext)
{
    return IsAdmin(httpContext)
        ? null
        : Results.StatusCode(StatusCodes.Status403Forbidden);
}

public partial class Program { }

record RegisterRequest(string Email, string FullName, string Identification, string Password, string? Role);
record LoginRequest(string Email, string Password);
record ForgotPasswordRequest(string Email, string? FullName);
record UpdateUserRequest(string Email, string FullName, string Identification, string? Password, string? Role, bool? IsActive);

record UserRecord(Guid Id, string Email, string FullName, string Identification, string PasswordHash, string Role, DateTime CreatedAt, bool IsActive);

sealed class PasswordRecoveryNotifier
{
    private readonly IConfiguration _configuration;

    public PasswordRecoveryNotifier(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task NotifyAdminAsync(UserRecord user, string? requestedName)
    {
        var adminEmail = GetConfigValue("ADMIN_EMAIL", "AdminEmail") ?? "admin@muebles.com";
        var subject = "Solicitud de recuperacion de contrasena";
        var body = $"""
            El cliente solicito recuperar su contrasena.

            Nombre indicado: {ValueOrDefault(requestedName, user.FullName)}
            Nombre registrado: {user.FullName}
            Identificacion: {ValueOrDefault(user.Identification, "No registrada")}
            Correo: {user.Email}

            La contrasena anterior no puede enviarse porque esta almacenada cifrada.
            """;

        await SendEmailAsync(adminEmail, subject, body);
    }

    private async Task SendEmailAsync(string to, string subject, string body)
    {
        var host = GetConfigValue("SMTP_HOST", "Smtp:Host");
        if (string.IsNullOrWhiteSpace(host))
        {
            Console.WriteLine($"[PasswordRecovery] Para: {to}");
            Console.WriteLine($"[PasswordRecovery] Asunto: {subject}");
            Console.WriteLine(body);
            return;
        }

        var portValue = GetConfigValue("SMTP_PORT", "Smtp:Port");
        var port = int.TryParse(portValue, out var parsedPort) ? parsedPort : 587;
        var username = GetConfigValue("SMTP_USER", "Smtp:User");
        var password = GetConfigValue("SMTP_PASSWORD", "Smtp:Password");
        var from = GetConfigValue("SMTP_FROM", "Smtp:From") ?? username ?? "no-reply@muebles.com";

        using var message = new MailMessage(from, to, subject, body);
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = string.Equals(GetConfigValue("SMTP_SSL", "Smtp:Ssl"), "true", StringComparison.OrdinalIgnoreCase)
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        await client.SendMailAsync(message);
    }

    private string? GetConfigValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string ValueOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}

sealed class AuthDb
{
    private readonly string _connectionString;

    public AuthDb(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Email TEXT NOT NULL UNIQUE,
                FullName TEXT NOT NULL,
                Identification TEXT NOT NULL DEFAULT '',
                PasswordHash TEXT NOT NULL,
                Role TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                IsActive INTEGER NOT NULL
            );
        ";
        command.ExecuteNonQuery();

        EnsureIdentificationColumn(connection);
        SeedDefaultAdmin(connection);
    }

    public void CreateUser(UserRecord user)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Users (Id, Email, FullName, Identification, PasswordHash, Role, CreatedAt, IsActive)
            VALUES ($id, $email, $fullName, $identification, $passwordHash, $role, $createdAt, $isActive);
        ";
        command.Parameters.AddWithValue("$id", user.Id.ToString());
        command.Parameters.AddWithValue("$email", user.Email);
        command.Parameters.AddWithValue("$fullName", user.FullName);
        command.Parameters.AddWithValue("$identification", user.Identification);
        command.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$role", user.Role);
        command.Parameters.AddWithValue("$createdAt", user.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$isActive", user.IsActive ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public UserRecord? GetUserById(Guid id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Email, FullName, Identification, PasswordHash, Role, CreatedAt, IsActive
            FROM Users
            WHERE Id = $id
            LIMIT 1;
        ";
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return MapUser(reader);
    }

    public UserRecord? GetUserByEmail(string email)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Email, FullName, Identification, PasswordHash, Role, CreatedAt, IsActive
            FROM Users
            WHERE Email = $email
            LIMIT 1;
        ";
        command.Parameters.AddWithValue("$email", email);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return MapUser(reader);
    }

    public List<UserRecord> GetUsers()
    {
        var users = new List<UserRecord>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Email, FullName, Identification, PasswordHash, Role, CreatedAt, IsActive
            FROM Users
            ORDER BY CreatedAt DESC;
        ";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            users.Add(MapUser(reader));
        }

        return users;
    }

    public void UpdateUser(UserRecord user)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Users
            SET Email = $email,
                FullName = $fullName,
                Identification = $identification,
                PasswordHash = $passwordHash,
                Role = $role,
                IsActive = $isActive
            WHERE Id = $id;
        ";
        command.Parameters.AddWithValue("$id", user.Id.ToString());
        command.Parameters.AddWithValue("$email", user.Email);
        command.Parameters.AddWithValue("$fullName", user.FullName);
        command.Parameters.AddWithValue("$identification", user.Identification);
        command.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$role", user.Role);
        command.Parameters.AddWithValue("$isActive", user.IsActive ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public void DeleteUser(Guid id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Users WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    private static UserRecord MapUser(IDataRecord record)
    {
        return new UserRecord(
            Guid.Parse(record.GetString(0)),
            record.GetString(1),
            record.GetString(2),
            record.GetString(3),
            record.GetString(4),
            record.GetString(5),
            DateTime.Parse(record.GetString(6)),
            record.GetInt32(7) == 1);
    }

    private static void EnsureIdentificationColumn(SqliteConnection connection)
    {
        using var infoCommand = connection.CreateCommand();
        infoCommand.CommandText = "PRAGMA table_info(Users);";

        var hasIdentification = false;
        using (var reader = infoCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "Identification", StringComparison.OrdinalIgnoreCase))
                {
                    hasIdentification = true;
                    break;
                }
            }
        }

        if (hasIdentification)
        {
            return;
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "ALTER TABLE Users ADD COLUMN Identification TEXT NOT NULL DEFAULT '';";
        alterCommand.ExecuteNonQuery();
    }

    private static void SeedDefaultAdmin(SqliteConnection connection)
    {
        using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT COUNT(1) FROM Users WHERE Email = $email;";
        existsCommand.Parameters.AddWithValue("$email", "admin@muebles.com");
        var exists = Convert.ToInt32(existsCommand.ExecuteScalar()) > 0;
        if (exists)
        {
            return;
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = @"
            INSERT INTO Users (Id, Email, FullName, Identification, PasswordHash, Role, CreatedAt, IsActive)
            VALUES ($id, $email, $fullName, $identification, $passwordHash, $role, $createdAt, $isActive);
        ";
        insertCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        insertCommand.Parameters.AddWithValue("$email", "admin@muebles.com");
        insertCommand.Parameters.AddWithValue("$fullName", "Administrador");
        insertCommand.Parameters.AddWithValue("$identification", "admin");
        insertCommand.Parameters.AddWithValue("$passwordHash", BCrypt.Net.BCrypt.HashPassword("Admin123*"));
        insertCommand.Parameters.AddWithValue("$role", "Admin");
        insertCommand.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
        insertCommand.Parameters.AddWithValue("$isActive", 1);
        insertCommand.ExecuteNonQuery();
    }
}
