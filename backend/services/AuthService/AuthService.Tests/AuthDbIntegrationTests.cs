using BCrypt.Net;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuthService.Tests;

public sealed class AuthDbIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly AuthDb _db;

    public AuthDbIntegrationTests()
    {
        // PREPARAR: cada prueba arranca con una base SQLite limpia (equivalente a H2 in-memory del Word)
        _dbPath = Path.Combine(Path.GetTempPath(), $"auth-test-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath};Pooling=False";
        _db = new AuthDb(_connectionString);
        _db.Initialize();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* archivo bloqueado en CI, ignorar */ }
        }
    }

    // --- PRUEBA 1: la inicializacion siembra al admin por defecto ---
    [Fact(DisplayName = "Integracion: Initialize siembra usuario admin por defecto")]
    public void Initialize_AlwaysSeedsDefaultAdminUser()
    {
        // Act
        var admin = _db.GetUserByEmail("admin@muebles.com");

        // Assert
        Assert.NotNull(admin);
        Assert.Equal("Admin", admin!.Role);
        Assert.True(admin.IsActive);
    }

    // --- PRUEBA 2: crear usuario y consultarlo por email ---
    [Fact(DisplayName = "Integracion: CreateUser persiste y GetUserByEmail retorna el registro")]
    public void CreateUser_ThenGetUserByEmail_ReturnsCreatedUser()
    {
        // Arrange
        var user = new UserRecord(
            Guid.NewGuid(),
            "cliente@muebles.com",
            "Cliente Demo",
            "1234567890",
            BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Customer",
            DateTime.UtcNow,
            true);

        // Act
        _db.CreateUser(user);
        var fetched = _db.GetUserByEmail("cliente@muebles.com");

        // Assert
        Assert.NotNull(fetched);
        Assert.Equal(user.Id, fetched!.Id);
        Assert.Equal("Cliente Demo", fetched.FullName);
        Assert.Equal("Customer", fetched.Role);
    }

    // --- PRUEBA 3: actualizar usuario cambia email y rol persistidos ---
    [Fact(DisplayName = "Integracion: UpdateUser cambia los campos y se reflejan en GetUserById")]
    public void UpdateUser_ChangesFieldsPersisted()
    {
        // Arrange
        var original = new UserRecord(
            Guid.NewGuid(),
            "old@muebles.com",
            "Nombre Original",
            "0000000001",
            BCrypt.Net.BCrypt.HashPassword("oldpass"),
            "Customer",
            DateTime.UtcNow,
            true);
        _db.CreateUser(original);

        var updated = original with
        {
            Email = "new@muebles.com",
            FullName = "Nombre Nuevo",
            Role = "Admin",
            IsActive = false
        };

        // Act
        _db.UpdateUser(updated);
        var fetched = _db.GetUserById(original.Id);

        // Assert
        Assert.NotNull(fetched);
        Assert.Equal("new@muebles.com", fetched!.Email);
        Assert.Equal("Nombre Nuevo", fetched.FullName);
        Assert.Equal("Admin", fetched.Role);
        Assert.False(fetched.IsActive);
    }

    // --- PRUEBA 4: eliminar usuario lo deja sin rastro en la BD ---
    [Fact(DisplayName = "Integracion: DeleteUser elimina al usuario y GetUserById retorna null")]
    public void DeleteUser_RemovesUserFromDatabase()
    {
        // Arrange
        var user = new UserRecord(
            Guid.NewGuid(),
            "borrar@muebles.com",
            "Para borrar",
            "9999999999",
            BCrypt.Net.BCrypt.HashPassword("temporal"),
            "Customer",
            DateTime.UtcNow,
            true);
        _db.CreateUser(user);
        Assert.NotNull(_db.GetUserById(user.Id));

        // Act
        _db.DeleteUser(user.Id);

        // Assert
        Assert.Null(_db.GetUserById(user.Id));
        Assert.Null(_db.GetUserByEmail("borrar@muebles.com"));
    }
}
