using CatalogService.Api;
using CatalogService.Application;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["DATABASE_URL"]
                       ?? "Host=proyecto-muebles-postgres;Port=5432;Database=muebles_db;Username=postgres;Password=postgres";

builder.Services.AddSingleton<IProductRepository>(new CatalogDbProductRepository(connectionString));
builder.Services.AddSingleton<ProductCatalogService>();

var app = builder.Build();

app.MapGet("/api/catalog", CatalogEndpoints.GetCatalog);
app.MapPost("/api/catalog/validate", CatalogEndpoints.ValidateProduct);

app.Run();
