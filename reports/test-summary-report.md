# Reporte resumen de pruebas

Fecha de ejecucion: 2026-06-15 11:35 America/Bogota

## Resultado general

| Componente | Tipo de prueba | Resultado | Total |
| --- | --- | --- | --- |
| CatalogService backend | Unitarias, integrales y sistema | OK | 11/11 |
| Frontend React | Componentes, flujos UI y E2E | OK | 8/8 |
| Checkout E2E | Compra completa con orden, pago y factura | OK | 1/1 |

## Evidencias generadas

| Evidencia | Archivo |
| --- | --- |
| Reporte tecnico backend TRX | `reports/backend-test-results/catalog-tests.trx` |
| Cobertura backend Cobertura XML | `reports/backend-test-results/afdb75bd-f3a2-4696-b6cd-d93ecd6230d4/coverage.cobertura.xml` |
| Reporte funcional E2E de compra | `reports/checkout-e2e-report.md` |

## Backend CatalogService

Comando ejecutado:

```powershell
dotnet test backend/services/CatalogService/CatalogService.Tests/CatalogService.Tests.csproj --logger "trx;LogFileName=catalog-tests.trx" --results-directory reports/backend-test-results --collect:"XPlat Code Coverage"
```

Resultado:

```text
Correctas! - Con error: 0, Superado: 11, Omitido: 0, Total: 11
```

Pruebas cubiertas:

- Unitarias: validacion de producto completo, precio limite, nombre obligatorio y errores acumulados.
- Integrales: colaboracion entre `ProductCatalogService` e `InMemoryProductRepository`.
- Sistema: contrato esperado para validacion de catalogo con respuesta equivalente a `200` y `400`.

## Frontend React

Comando ejecutado:

```powershell
cd frontend
npm.cmd test -- --run
```

Resultado:

```text
Test Files  2 passed
Tests       8 passed
```

Pruebas cubiertas:

- Render inicial y login.
- Opciones de cliente y administrador.
- CRUD de inventario.
- Flujos de ordenes y pagos.
- Bloqueo de checkout para invitado.
- Usuarios, carrito y persistencia de sesion.
- E2E de compra completa.

## Checkout E2E

El E2E genero un reporte funcional en:

```text
reports/checkout-e2e-report.md
```

Datos principales de la ultima ejecucion:

- Cliente: Cliente compra 32cc4154
- Correo: cliente.32cc4154@muebles.test
- Producto: Producto compra 7de2623b
- Orden: 07728489-a135-4c27-8102-0c41974d1e20
- Pago: 2af59582-d6e3-4c4e-9c26-700d60af0d43
- Factura: FAC-E2E-2af59582
- Total pagado: $2088.00

## Pipeline GitHub Actions

El pipeline tambien deja reporte del backend como artifact.

Workflow:

```text
.github/workflows/local-ci.yml
```

Artifacts:

- `backend-test-results`: contiene los resultados `.trx` del job backend.

Checks esperados en GitHub:

- `Backend .NET`: restore, build y tests backend.
- `Frontend React`: npm ci, tests frontend y build frontend.

## Criterio de cierre

Las pruebas quedan reportadas cuando existen estos archivos:

- `reports/test-summary-report.md`
- `reports/backend-test-results/catalog-tests.trx`
- `reports/backend-test-results/**/coverage.cobertura.xml`
- `reports/checkout-e2e-report.md`
