# Proyecto Muebles Modernos 

Sistema web para una tienda de muebles modernos. El proyecto permite consultar catalogo, iniciar sesion, registrar usuarios, administrar inventario, agregar productos al carrito, crear ordenes, autorizar pagos y generar factura.

El repositorio esta organizado como un monorepo con frontend React, gateway Node.js y backend .NET 8 por servicios.

## Caracteristicas principales

- Interfaz web para clientes y administradores.
- Login, registro y recuperacion de contrasena.
- Catalogo de productos de muebles.
- Carrito de compras por cliente.
- Checkout con creacion de orden.
- Autorizacion de pago y generacion de factura.
- Administracion de productos de inventario.
- Administracion basica de usuarios.
- API Gateway para centralizar la comunicacion del frontend.
- Microservicios .NET separados por responsabilidad.
- Pruebas unitarias, integrales y E2E simulado.
- Reportes de pruebas y captura de evidencia E2E real.

## Tecnologias usadas

| Capa | Tecnologia | Uso |
| --- | --- | --- |
| Frontend | React 18 | Construccion de interfaz por componentes |
| Frontend | Vite | Servidor de desarrollo y build |
| Frontend | TypeScript | Tipado de componentes, servicios y modelos |
| Frontend | Tailwind CSS | Estilos de la aplicacion |
| Pruebas frontend | Vitest + Testing Library | Pruebas de UI, formularios y flujo E2E simulado |
| Gateway | Node.js + Express | Proxy/BFF entre frontend y microservicios |
| Backend | .NET 8 | APIs de negocio |
| Backend | Minimal APIs | Endpoints HTTP ligeros |
| Pruebas backend | xUnit + coverlet | Pruebas unitarias, integrales y cobertura |
| Base de datos | PostgreSQL | Persistencia principal en entorno Docker |
| Auth local | SQLite / archivo local segun ejecucion | Persistencia local del servicio de autenticacion |
| Infraestructura | Docker Compose | Levantar base de datos, servicios y gateway |

## Estructura del proyecto

```text
Proyecto-Muebles4-main/
+-- backend/
|   +-- 1-customer-experience/
|   +-- 2-order-management/
|   +-- 3-inventory-product/
|   +-- 4-customer-loyalty/
|   +-- 5-admin-analytics/
|   +-- node-api-gateway/
|   +-- services/
|       +-- AuthService/
|       +-- CatalogService/
|       +-- CartService/
|       +-- InventoryService/
|       +-- OrderService/
|       +-- PaymentService/
+-- frontend/
|   +-- src/
|       +-- components/
|       +-- services/
|       +-- validation/
|       +-- mocks/
|       +-- App.tsx
+-- shared/
+-- docs/
+-- reports/
+-- scripts/
+-- docker-compose.yml
```

## Organizacion por modulos de negocio

| Modulo | Responsabilidad | Servicios/funciones |
| --- | --- | --- |
| `1-customer-experience` | Experiencia directa del cliente | Catalogo, configurador, carrito, checkout, CMS |
| `2-order-management` | Ciclo de compra | Ordenes, pagos, envios, notificaciones |
| `3-inventory-product` | Productos y stock | Inventario, productos, precios, proveedores |
| `4-customer-loyalty` | Identidad y relacion con cliente | Autenticacion, usuarios, resenas, wishlist, soporte |
| `5-admin-analytics` | Operacion interna | Administracion, analitica, marketing, integraciones |

## Servicios disponibles

| Servicio | Puerto | Funcion principal |
| --- | --- | --- |
| PostgreSQL | `5432` | Base de datos principal |
| `AuthService` | `8081` | Registro, login, recuperacion de contrasena y usuarios |
| `CatalogService` | `8082` | Consulta de catalogo y validacion de productos |
| `CartService` | `8083` | Consulta, agregado, eliminacion y limpieza del carrito |
| `OrderService` | `8084` | Creacion, consulta, actualizacion y eliminacion de ordenes |
| `PaymentService` | `8085` | Autorizacion de pagos, consulta de pagos y factura PDF |
| `InventoryService` | `8086` | CRUD de productos de inventario |
| API Gateway | `9090` | Entrada unica del frontend hacia los servicios |
| Frontend Vite | `5173` | Aplicacion web React |

## Funciones por servicio

### AuthService

- Registrar usuario.
- Iniciar sesion.
- Recuperar contrasena.
- Listar usuarios.
- Actualizar usuario.
- Eliminar usuario.

Rutas expuestas por el gateway:

```text
POST   /api/auth/login
POST   /api/auth/register
POST   /api/auth/forgot-password
GET    /api/auth/users
PUT    /api/auth/users/:id
DELETE /api/auth/users/:id
```

### CatalogService

- Consultar productos visibles en el catalogo.
- Validar reglas de producto.
- Aplicar reglas de dominio como nombre obligatorio y precio mayor que cero.

Rutas expuestas:

```text
GET /api/catalog
```

### CartService

- Consultar carrito de un cliente.
- Agregar producto al carrito.
- Eliminar un producto del carrito.
- Limpiar todos los productos del carrito.

Rutas expuestas:

```text
GET    /api/cart/:customerId
POST   /api/cart/items
DELETE /api/cart/:customerId/items
DELETE /api/cart/:customerId/items/:productId
```

### OrderService

- Crear orden de compra.
- Consultar ordenes.
- Consultar orden por id.
- Actualizar estado de orden.
- Eliminar orden.

Rutas expuestas:

```text
GET    /api/orders
GET    /api/orders/:orderId
POST   /api/orders
PUT    /api/orders/:orderId
DELETE /api/orders/:orderId
```

### PaymentService

- Autorizar pago.
- Consultar pagos.
- Consultar pago por id.
- Actualizar pago.
- Eliminar pago.
- Descargar factura en PDF.

Rutas expuestas:

```text
GET    /api/payments
GET    /api/payments/:paymentId
GET    /api/payments/:paymentId/invoice/pdf
POST   /api/payments/authorize
PUT    /api/payments/:paymentId
DELETE /api/payments/:paymentId
```

### InventoryService

- Listar productos de inventario.
- Consultar producto por id.
- Crear producto.
- Actualizar producto.
- Eliminar producto.

Rutas expuestas:

```text
GET    /api/inventory/products
GET    /api/inventory/products/:productId
POST   /api/inventory/products
PUT    /api/inventory/products/:productId
DELETE /api/inventory/products/:productId
```

## Arquitectura del flujo

```text
Usuario
  |
  v
Frontend React (http://localhost:5173)
  |
  v
API Gateway Node (http://localhost:9090)
  |
  v
Microservicios .NET
  |
  v
PostgreSQL / repositorios internos
  |
  v
Respuesta al frontend
```

El frontend no llama directamente a cada microservicio. Consume el gateway en `http://localhost:9090`, y el gateway redirige cada peticion al servicio correspondiente.

## Arquitectura limpia en CatalogService

`CatalogService` es el servicio con estructura mas completa para mostrar arquitectura limpia:

```text
backend/services/CatalogService/
+-- CatalogService.Api/
+-- CatalogService.Application/
+-- CatalogService.Domain/
+-- CatalogService.Infrastructure/
+-- CatalogService.Tests/
```

| Capa | Responsabilidad |
| --- | --- |
| `Api` | Expone endpoints HTTP |
| `Application` | Casos de uso y logica de aplicacion |
| `Domain` | Entidades y reglas puras de negocio |
| `Infrastructure` | Repositorios e implementaciones tecnicas |
| `Tests` | Pruebas automatizadas |

## Flujo funcional principal

1. El usuario abre la aplicacion web.
2. El frontend consulta el catalogo.
3. El usuario inicia sesion o se registra.
4. El usuario agrega un producto al carrito.
5. El checkout crea una orden.
6. El sistema autoriza el pago.
7. Se genera una factura.
8. La interfaz muestra confirmacion de compra.

## Requisitos

- Node.js 20 o superior.
- npm.
- .NET SDK 8.
- Docker y Docker Compose.

## Ejecucion local

### 1. Levantar backend, gateway y base de datos

Desde la raiz del proyecto:

```powershell
docker compose up --build
```

Esto levanta PostgreSQL, los servicios .NET y el API Gateway.

### 2. Levantar frontend

En otra terminal:

```powershell
cd frontend
npm.cmd install
npm.cmd run dev
```

Abrir:

```text
http://localhost:5173
```

## Usuario de prueba

Si no existe un usuario, se puede crear desde el frontend o consumiendo el endpoint de registro:

```bash
curl -X POST http://localhost:9090/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "cliente@muebles.com",
    "fullName": "Cliente Demo",
    "identification": "1234567890",
    "password": "Password123!"
  }'
```

Luego iniciar sesion con:

```text
email: cliente@muebles.com
password: Password123!
```

## Pruebas

### Backend

```powershell
dotnet test backend\services\CatalogService\CatalogService.Tests\CatalogService.Tests.csproj
```

### Frontend

```powershell
cd frontend
npm.cmd test -- --run
```

### Reporte unificado

```powershell
powershell -ExecutionPolicy Bypass -File scripts\run-test-report.ps1
```

## Captura E2E real

La aplicacion permite activar captura de llamadas reales para evidenciar el flujo completo.

Abrir:

```text
http://localhost:5173?e2eCapture=1
```

Luego realizar el flujo:

1. Login.
2. Consulta de catalogo.
3. Agregar producto al carrito.
4. Crear orden.
5. Autorizar pago.
6. Descargar reporte desde el panel de captura.

## Documentacion adicional

- `docs/architecture.md`
- `docs/modules.md`
- `docs/estrategia_pruebas_unitarias_integrales_e2e.md`
- `docs/guia_tecnologia_estructura_funcionamiento.md`
- `docs/guia_casos_prueba_presentacion.md`
- `docs/guia_validacion_pruebas_y_pipeline.md`
- `docs/pruebas_catalogo.md`
- ## En el cuerpo o estructura se cuenta con
- defectos// unidad 6
- pruebas de cargas expuextas en clase
- pruebas unitarias, integrales
- el github tiene los worflow de los pipelines implementados
- 

## Estado del proyecto

El proyecto ya cuenta con una base funcional integrada entre frontend, gateway y servicios principales. Tambien incluye pruebas automatizadas para reglas de dominio, integracion de CatalogService, validaciones frontend y flujo E2E simulado. El siguiente paso natural es automatizar E2E real con navegador usando Playwright o Cypress.
# Muebles-unisabana
# Muebles-unisabana
