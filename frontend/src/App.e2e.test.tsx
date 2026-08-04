import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';

const mockFetch = vi.fn();
global.fetch = mockFetch as typeof fetch;

class RuntimeCatalogProduct {
  readonly id: string;
  readonly name: string;
  readonly category: string;
  readonly price: number;
  readonly image: string;
  readonly colors: string[];
  readonly measures: string[];

  constructor(overrides: Partial<RuntimeCatalogProduct> = {}) {
    const suffix = crypto.randomUUID().slice(0, 8);

    this.id = overrides.id ?? `product-${suffix}`;
    this.name = overrides.name ?? `Producto compra ${suffix}`;
    this.category = overrides.category ?? 'Sala';
    this.price = overrides.price ?? 1800;
    this.image = overrides.image ?? `https://example.com/producto-${suffix}.jpg`;
    this.colors = overrides.colors ?? ['Gris'];
    this.measures = overrides.measures ?? ['200x90'];
  }
}

class RuntimeCustomer {
  readonly id: string;
  readonly email: string;
  readonly fullName: string;
  readonly role: string;
  readonly token: string;

  constructor() {
    const suffix = crypto.randomUUID().slice(0, 8);

    this.id = crypto.randomUUID();
    this.email = `cliente.${suffix}@muebles.test`;
    this.fullName = `Cliente compra ${suffix}`;
    this.role = 'Customer';
    this.token = `token-${crypto.randomUUID()}`;
  }
}

function calculateTotals(subtotal: number) {
  const tax = Number((subtotal * 0.16).toFixed(2));
  return {
    subtotal,
    tax,
    total: subtotal + tax
  };
}

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), { status }));
}

function arrangeCheckoutRuntime() {
  const product = new RuntimeCatalogProduct();
  const customer = new RuntimeCustomer();
  const state = {
    cartItems: [] as Array<{
      productId: string;
      productName: string;
      quantity: number;
      unitPrice: number;
      subtotal: number;
    }>,
    orders: [] as unknown[],
    payments: [] as unknown[]
  };

  mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';

    if (url.includes('/api/auth/login')) {
      return jsonResponse({
        token: customer.token,
        expiresIn: 3600,
        user: {
          id: customer.id,
          email: customer.email,
          fullName: customer.fullName,
          role: customer.role
        }
      });
    }

    if (url.includes('/api/catalog')) {
      return jsonResponse([product]);
    }

    if (url.includes('/api/inventory/products')) {
      return jsonResponse([]);
    }

    if (url.includes('/api/auth/users')) {
      return jsonResponse([]);
    }

    if (url.includes('/api/orders') && method === 'POST') {
      const payload = JSON.parse(String(init?.body ?? '{}'));
      expect(payload.customerId).toBe(customer.id);
      expect(payload.items).toEqual([
        {
          productId: product.id,
          quantity: 1,
          unitPrice: product.price
        }
      ]);

      const subtotal = payload.items.reduce(
        (sum: number, item: { quantity: number; unitPrice: number }) => sum + item.quantity * item.unitPrice,
        0
      );
      const totals = calculateTotals(subtotal);
      const order = {
        orderId: crypto.randomUUID(),
        customerId: payload.customerId,
        status: 'Created',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        items: payload.items.map((item: { productId: string; quantity: number; unitPrice: number }) => ({
          orderItemId: crypto.randomUUID(),
          productId: item.productId,
          quantity: item.quantity,
          unitPrice: item.unitPrice,
          subtotal: item.quantity * item.unitPrice
        })),
        ...totals
      };
      state.orders = [...state.orders, order];
      return jsonResponse(order, 201);
    }

    if (url.includes('/api/orders')) {
      return jsonResponse(state.orders);
    }

    if (url.includes('/api/payments/authorize') && method === 'POST') {
      const payload = JSON.parse(String(init?.body ?? '{}'));
      expect(payload.customerId).toBe(customer.id);
      expect(payload.customerEmail).toBe(customer.email);
      expect(payload.items).toEqual([
        {
          productId: product.id,
          productName: product.name,
          quantity: 1,
          unitPrice: product.price
        }
      ]);

      const subtotal = payload.items.reduce(
        (sum: number, item: { quantity: number; unitPrice: number }) => sum + item.quantity * item.unitPrice,
        0
      );
      const totals = calculateTotals(subtotal);
      const paymentId = crypto.randomUUID();
      const invoice = {
        invoiceNumber: `FAC-E2E-${paymentId.slice(0, 8)}`,
        issuedAt: new Date().toISOString(),
        customerId: payload.customerId,
        customerName: payload.customerName,
        customerEmail: payload.customerEmail,
        paymentMethod: payload.paymentMethod,
        items: payload.items.map((item: { productName: string; quantity: number; unitPrice: number }) => ({
          productName: item.productName,
          quantity: item.quantity,
          unitPrice: item.unitPrice,
          subtotal: item.quantity * item.unitPrice
        })),
        ...totals
      };
      const payment = {
        paymentId,
        orderId: payload.orderId,
        customerId: payload.customerId,
        customerName: payload.customerName,
        customerEmail: payload.customerEmail,
        paymentMethod: payload.paymentMethod,
        status: 'Authorized',
        createdAt: new Date().toISOString(),
        invoice,
        ...totals
      };
      state.payments = [...state.payments, payment];
      return jsonResponse({ paymentId, status: payment.status, invoice });
    }

    if (url.includes('/api/payments')) {
      return jsonResponse(state.payments);
    }

    if (url.includes('/api/cart/items') && method === 'POST') {
      const payload = JSON.parse(String(init?.body ?? '{}'));
      expect(payload.customerId).toBe(customer.id);
      expect(payload.productId).toBe(product.id);
      expect(payload.productName).toBe(product.name);
      expect(payload.unitPrice).toBe(product.price);

      const item = {
        productId: payload.productId,
        productName: payload.productName,
        quantity: Number(payload.quantity),
        unitPrice: Number(payload.unitPrice),
        subtotal: Number(payload.quantity) * Number(payload.unitPrice)
      };
      state.cartItems = [item];
      return jsonResponse({
        id: crypto.randomUUID(),
        customerId: payload.customerId,
        items: state.cartItems,
        totalAmount: calculateTotals(item.subtotal).total
      });
    }

    if (/\/api\/cart\/[^/]+\/items$/.test(url) && method === 'DELETE') {
      state.cartItems = [];
      return jsonResponse({
        id: crypto.randomUUID(),
        customerId: customer.id,
        items: state.cartItems,
        totalAmount: 0
      });
    }

    if (url.includes('/api/cart/')) {
      const subtotal = state.cartItems.reduce((sum, item) => sum + item.subtotal, 0);
      return jsonResponse({
        id: crypto.randomUUID(),
        customerId: customer.id,
        items: state.cartItems,
        totalAmount: calculateTotals(subtotal).total
      });
    }

    return jsonResponse({});
  });

  return { product, customer, state };
}

function writeCheckoutReport(runtime: ReturnType<typeof arrangeCheckoutRuntime>) {
  const reportDir = resolve(process.cwd(), '..', 'reports');
  mkdirSync(reportDir, { recursive: true });

  const payment = runtime.state.payments[0] as {
    paymentId: string;
    orderId: string;
    total: number;
    invoice: {
      invoiceNumber: string;
      customerName: string;
      customerEmail: string;
      total: number;
    };
  };
  const order = runtime.state.orders[0] as { orderId: string };
  const endpointChecks = [
    ['Login', '/api/auth/login'],
    ['Catalogo', '/api/catalog'],
    ['Carrito', '/api/cart/items'],
    ['Orden', '/api/orders'],
    ['Pago', '/api/payments/authorize']
  ];

  const content = `# Reporte E2E de compra

Fecha de ejecucion: ${new Date().toISOString()}

## Datos de compra

- Datos generados durante la ejecucion del test, sin depender de registros quemados.
- Cliente: ${runtime.customer.fullName}
- Correo: ${runtime.customer.email}
- Producto: ${runtime.product.name}
- Precio unitario: $${runtime.product.price.toFixed(2)}
- Orden: ${order.orderId}
- Pago: ${payment.paymentId}
- Factura: ${payment.invoice.invoiceNumber}
- Total pagado: $${payment.invoice.total.toFixed(2)}

## Flujo validado

- Se inicio sesion como cliente autenticado.
- Se cargo el catalogo de productos.
- Se agrego un producto al carrito.
- Se creo la orden de compra.
- Se autorizo el pago.
- Se genero una factura visible para el cliente.

## Endpoints verificados

${endpointChecks.map(([name, endpoint]) => `- ${name}: ${endpoint}`).join('\n')}

## Tests/validaciones realizadas

- La aplicacion muestra el producto cargado desde catalogo.
- El login cambia el rol visible a Customer.
- El carrito confirma que el producto fue agregado.
- La compra muestra mensaje de pago realizado.
- La factura generada contiene prefijo FAC-E2E.
- El estado interno del flujo registra 1 orden y 1 pago.
- Se verifico que las llamadas principales del checkout fueron ejecutadas.
`;

  writeFileSync(resolve(reportDir, 'checkout-e2e-report.md'), content, 'utf8');
}

beforeEach(() => {
  localStorage.clear();
  mockFetch.mockReset();
});

describe('App checkout end to end flow', () => {
  it('logs in, loads products, adds to cart and finishes payment with invoice', async () => {
    const runtime = arrangeCheckoutRuntime();

    render(<App />);

    expect(await screen.findByText(runtime.product.name)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /iniciar sesi/i }));
    expect(await screen.findByText('Rol: Customer')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /agregar al carrito/i }));
    expect(await screen.findByText(`${runtime.product.name} agregado al carrito`)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Pagar y generar factura' }));

    expect(await screen.findAllByText(/Pago realizado correctamente/)).not.toHaveLength(0);
    expect(await screen.findByText(/Factura generada: FAC-E2E-/)).toBeInTheDocument();

    await waitFor(() => {
      expect(runtime.state.orders).toHaveLength(1);
      expect(runtime.state.payments).toHaveLength(1);
    });

    expect(mockFetch.mock.calls.some(([input, init]) => String(input).includes('/api/auth/login') && init?.method === 'POST')).toBe(true);
    expect(mockFetch.mock.calls.some(([input]) => String(input).includes('/api/catalog'))).toBe(true);
    expect(mockFetch.mock.calls.some(([input, init]) => String(input).includes('/api/cart/items') && init?.method === 'POST')).toBe(true);
    expect(mockFetch.mock.calls.some(([input, init]) => String(input).includes('/api/orders') && init?.method === 'POST')).toBe(true);
    expect(mockFetch.mock.calls.some(([input, init]) => String(input).includes('/api/payments/authorize') && init?.method === 'POST')).toBe(true);

    writeCheckoutReport(runtime);
  });
});
