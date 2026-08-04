# Reporte E2E de compra

Fecha de ejecucion: 2026-06-15T20:38:48.503Z

## Datos de compra

- Datos generados durante la ejecucion del test, sin depender de registros quemados.
- Cliente: Cliente compra b23b8ddc
- Correo: cliente.b23b8ddc@muebles.test
- Producto: Producto compra 50aadf3c
- Precio unitario: $1800.00
- Orden: 5db50522-2c9c-47c5-8584-3e9370292831
- Pago: ab174bf8-0481-41f6-b0cf-946af8c3f548
- Factura: FAC-E2E-ab174bf8
- Total pagado: $2088.00

## Flujo validado

- Se inicio sesion como cliente autenticado.
- Se cargo el catalogo de productos.
- Se agrego un producto al carrito.
- Se creo la orden de compra.
- Se autorizo el pago.
- Se genero una factura visible para el cliente.

## Endpoints verificados

- Login: /api/auth/login
- Catalogo: /api/catalog
- Carrito: /api/cart/items
- Orden: /api/orders
- Pago: /api/payments/authorize

## Tests/validaciones realizadas

- La aplicacion muestra el producto cargado desde catalogo.
- El login cambia el rol visible a Customer.
- El carrito confirma que el producto fue agregado.
- La compra muestra mensaje de pago realizado.
- La factura generada contiene prefijo FAC-E2E.
- El estado interno del flujo registra 1 orden y 1 pago.
- Se verifico que las llamadas principales del checkout fueron ejecutadas.
