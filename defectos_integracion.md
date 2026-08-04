# Registro de defectos de integracion

| ID | Tipo | Defecto | Evidencia | Estado |
| --- | --- | --- | --- | --- |
| DEF-001 | Integracion | El catalogo dependia directamente de PostgreSQL en `Program.cs`, dificultando pruebas service-repository. | Se separo `IProductRepository`, `ProductCatalogService` y `CatalogDbProductRepository`. | Resuelto |
| DEF-002 | Sistema | No existia endpoint controlado para validar respuestas JSON sin tocar base real. | Se agrego `POST /api/catalog/validate` y pruebas de `200 OK` / `400 BadRequest`. | Resuelto |
| DEF-003 | Unitaria | La entidad `Product` no tenia reglas de dominio ejecutables. | Se agrego `Product.Validate()` con pruebas de equivalencia y valores limite. | Resuelto |
