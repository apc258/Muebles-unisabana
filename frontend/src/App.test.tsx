import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { App } from './App';

const mockFetch = vi.fn();
global.fetch = mockFetch as typeof fetch;

let loginRole = 'Admin';
let loginEmail = 'cliente@muebles.com';
let loginFullName = 'Cliente Demo';
let loginCustomerId = '11111111-1111-1111-1111-111111111111';

let users = [
  {
    id: 'user-1',
    email: 'admin@muebles.com',
    fullName: 'Administrador',
    role: 'Admin',
    createdAt: '2026-05-22T10:00:00Z',
    isActive: true
  }
];

let inventoryProducts = [
  {
    productId: 'prod-1',
    sku: 'SKU-001',
    name: 'Sofá Oslo',
    category: 'Sala',
    price: 2499,
    image: 'https://example.com/sofa.jpg',
    colors: ['Gris'],
    measures: ['200x90'],
    available: 5,
    reserved: 1,
    supplierName: 'Proveedor Uno',
    createdAt: '2026-05-22T10:00:00Z',
    updatedAt: '2026-05-22T10:00:00Z'
  }
];

let orders = [
  {
    orderId: 'order-1',
    customerId: '11111111-1111-1111-1111-111111111111',
    status: 'Paid',
    subtotal: 2499,
    tax: 399.84,
    total: 2898.84,
    createdAt: '2026-05-22T10:00:00Z',
    updatedAt: '2026-05-22T10:00:00Z',
    items: [
      {
        orderItemId: 'order-item-1',
        productId: 'prod-1',
        quantity: 1,
        unitPrice: 2499,
        subtotal: 2499
      }
    ]
  }
];

let payments = [
  {
    paymentId: 'payment-1',
    orderId: 'order-1',
    customerId: '11111111-1111-1111-1111-111111111111',
    customerName: 'Cliente Demo',
    customerEmail: 'cliente@muebles.com',
    paymentMethod: 'Tarjeta',
    status: 'Authorized',
    subtotal: 2499,
    tax: 399.84,
    total: 2898.84,
    createdAt: '2026-05-22T10:00:00Z',
    invoice: {
      invoiceNumber: 'FAC-20260522-AAAA1111',
      issuedAt: '2026-05-22T10:00:00Z',
      items: [
        {
          productName: 'Sofá Oslo',
          quantity: 1,
          unitPrice: 2499,
          subtotal: 2499
        }
      ],
      subtotal: 2499,
      tax: 399.84,
      total: 2898.84
    }
  }
];

let cartItems = [
  {
    productId: 'prod-1',
    productName: 'SofÃ¡ Oslo',
    quantity: 1,
    unitPrice: 2499,
    subtotal: 2499
  }
];

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), { status }));
}

function calculateTotals(subtotal: number) {
  const tax = Number((subtotal * 0.16).toFixed(2));
  return {
    subtotal,
    tax,
    total: subtotal + tax
  };
}

function calculateCartTotal() {
  return calculateTotals(cartItems.reduce((total, item) => total + item.subtotal, 0)).total;
}

beforeEach(() => {
  mockFetch.mockReset();
  localStorage.clear();
  loginRole = 'Admin';
  loginEmail = 'cliente@muebles.com';
  loginFullName = 'Cliente Demo';
  loginCustomerId = '11111111-1111-1111-1111-111111111111';
  users = [
    {
      id: 'user-1',
      email: 'admin@muebles.com',
      fullName: 'Administrador',
      role: 'Admin',
      createdAt: '2026-05-22T10:00:00Z',
      isActive: true
    }
  ];
  inventoryProducts = [
    {
      productId: 'prod-1',
      sku: 'SKU-001',
      name: 'Sofá Oslo',
      category: 'Sala',
      price: 2499,
      image: 'https://example.com/sofa.jpg',
      colors: ['Gris'],
      measures: ['200x90'],
      available: 5,
      reserved: 1,
      supplierName: 'Proveedor Uno',
      createdAt: '2026-05-22T10:00:00Z',
      updatedAt: '2026-05-22T10:00:00Z'
    }
  ];
  orders = [
    {
      orderId: 'order-1',
      customerId: '11111111-1111-1111-1111-111111111111',
      status: 'Paid',
      subtotal: 2499,
      tax: 399.84,
      total: 2898.84,
      createdAt: '2026-05-22T10:00:00Z',
      updatedAt: '2026-05-22T10:00:00Z',
      items: [
        {
          orderItemId: 'order-item-1',
          productId: 'prod-1',
          quantity: 1,
          unitPrice: 2499,
          subtotal: 2499
        }
      ]
    }
  ];
  payments = [
    {
      paymentId: 'payment-1',
      orderId: 'order-1',
      customerId: '11111111-1111-1111-1111-111111111111',
      customerName: 'Cliente Demo',
      customerEmail: 'cliente@muebles.com',
      paymentMethod: 'Tarjeta',
      status: 'Authorized',
      subtotal: 2499,
      tax: 399.84,
      total: 2898.84,
      createdAt: '2026-05-22T10:00:00Z',
      invoice: {
        invoiceNumber: 'FAC-20260522-AAAA1111',
        issuedAt: '2026-05-22T10:00:00Z',
        items: [
          {
            productName: 'Sofá Oslo',
            quantity: 1,
            unitPrice: 2499,
            subtotal: 2499
          }
        ],
        subtotal: 2499,
        tax: 399.84,
        total: 2898.84
      }
    }
  ];
  cartItems = [
    {
      productId: 'prod-1',
      productName: 'SofÃ¡ Oslo',
      quantity: 1,
      unitPrice: 2499,
      subtotal: 2499
    }
  ];

  mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';

    if (url.includes('/api/catalog')) {
      return jsonResponse([
        {
          id: 'prod-1',
          name: 'Sofá Oslo',
          category: 'Sala',
          price: 2499,
          image: 'https://example.com/sofa.jpg',
          colors: ['Gris'],
          measures: ['200x90']
        }
      ]);
    }

    if (url.includes('/api/inventory/products') && method === 'POST') {
      const payload = JSON.parse(String(init?.body ?? '{}'));
      const created = {
        productId: 'prod-2',
        createdAt: '2026-05-23T10:00:00Z',
        updatedAt: '2026-05-23T10:00:00Z',
        ...payload
      };
      inventoryProducts = [...inventoryProducts, created];
      return jsonResponse(created, 201);
    }

    if (url.includes('/api/inventory/products/prod-1') && method === 'PUT') {
      const payload = JSON.parse(String(init?.body ?? '{}'));
      inventoryProducts = inventoryProducts.map((item) => item.productId === 'prod-1' ? { ...item, ...payload } : item);
      return jsonResponse(inventoryProducts[0]);
    }

    if (url.includes('/api/inventory/products/prod-1') && method === 'DELETE') {
      inventoryProducts = inventoryProducts.filter((item) => item.productId !== 'prod-1');
      return jsonResponse({ message: 'Producto eliminado' });
    }

    if (url.includes('/api/inventory/products')) {
      return jsonResponse(inventoryProducts);
    }

    if (url.includes('/api/auth/register') && method === 'POST') {
      const payload = JSON.parse(String(init?.body ?? '{}'));
      const created = {
        id: 'user-2',
        email: payload.email,
        fullName: payload.fullName,
        role: 'Customer',
        createdAt: '2026-05-23T10:00:00Z',
        isActive: true
      };
      users = [...users, created];
      return jsonResponse(created, 201);
    }

    if (url.includes('/api/auth/users/user-1') && method === 'PUT') {
      const payload = JSON.parse(String(init?.body ?? '{}'));
      users = users.map((item) => item.id === 'user-1' ? { ...item, ...payload, role: payload.role ?? item.role, isActive: payload.isActive ?? item.isActive } : item);
      return jsonResponse(users[0]);
    }

    if (url.includes('/api/auth/users/user-1') && method === 'DELETE') {
      users = users.filter((item) => item.id !== 'user-1');
      return jsonResponse({ message: 'Usuario eliminado' });
    }

    if (url.includes('/api/auth/login')) {
      return jsonResponse({
        token: 'token-demo',
        expiresIn: 3600,
        user: {
          id: loginCustomerId,
          email: loginEmail,
          fullName: loginFullName,
          role: loginRole
        }
      });
    }

    if (url.includes('/api/auth/users')) {
      return jsonResponse(users);
    }

    if (url.includes('/api/orders') && method === 'POST') {
      const payload = JSON.parse(String(init?.body ?? '{}'));
      const totals = calculateTotals(Number(payload.items[0].unitPrice) * Number(payload.items[0].quantity));
      const created = {
        orderId: 'order-2',
        customerId: payload.customerId,
        status: 'Created',
        subtotal: totals.subtotal,
        tax: totals.tax,
        total: totals.total,
        createdAt: '2026-05-23T10:00:00Z',
        updatedAt: '2026-05-23T10:00:00Z',
        items: [
          {
            orderItemId: 'order-item-2',
            productId: payload.items[0].productId,
            quantity: Number(payload.items[0].quantity),
            unitPrice: Number(payload.items[0].unitPrice),
            subtotal: Number(payload.items[0].unitPrice) * Number(payload.items[0].quantity)
          }
        ]
      };
      orders = [...orders, created];
      return jsonResponse(created, 201);
    }

    if (url.includes('/api/orders/order-1') && method === 'PUT') {
      const payload = JSON.parse(String(init?.body ?? '{}'));
      orders = orders.map((item) => item.orderId === 'order-1' ? { ...item, status: payload.status } : item);
      return jsonResponse(orders[0]);
    }

    if (url.includes('/api/orders/order-1') && method === 'DELETE') {
      orders = orders.filter((item) => item.orderId !== 'order-1');
      return jsonResponse({ message: 'Orden eliminada' });
    }

    if (url.includes('/api/orders')) {
      return jsonResponse(orders);
    }

    if (url.includes('/api/payments/authorize') && method === 'POST') {
      const payload = JSON.parse(String(init?.body ?? '{}'));
      const totals = calculateTotals(Number(payload.items[0].unitPrice) * Number(payload.items[0].quantity));
      const created = {
        paymentId: 'payment-2',
        orderId: payload.orderId,
        customerId: payload.customerId,
        customerName: payload.customerName,
        customerEmail: payload.customerEmail,
        paymentMethod: payload.paymentMethod,
        status: 'Authorized',
        subtotal: totals.subtotal,
        tax: totals.tax,
        total: totals.total,
        createdAt: '2026-05-23T10:00:00Z',
        invoice: {
          invoiceNumber: 'FAC-20260523-BBBB2222',
          issuedAt: '2026-05-23T10:00:00Z',
          items: [
            {
              productName: payload.items[0].productName,
              quantity: Number(payload.items[0].quantity),
              unitPrice: Number(payload.items[0].unitPrice),
              subtotal: Number(payload.items[0].unitPrice) * Number(payload.items[0].quantity)
            }
          ],
          subtotal: totals.subtotal,
          tax: totals.tax,
          total: totals.total
        }
      };
      payments = [...payments, created];
      return jsonResponse({ paymentId: created.paymentId, status: created.status, invoice: created.invoice });
    }

    if (url.includes('/api/payments/payment-1') && method === 'PUT') {
      const payload = JSON.parse(String(init?.body ?? '{}'));
      payments = payments.map((item) => item.paymentId === 'payment-1' ? { ...item, status: payload.status, paymentMethod: payload.paymentMethod } : item);
      return jsonResponse(payments[0]);
    }

    if (url.includes('/api/payments/payment-1') && method === 'DELETE') {
      payments = payments.filter((item) => item.paymentId !== 'payment-1');
      return jsonResponse({ message: 'Pago eliminado' });
    }

    if (url.includes('/api/payments')) {
      return jsonResponse(payments);
    }

    if (url.includes('/api/cart/items') && method === 'POST') {
      cartItems = [
        {
          productId: 'prod-1',
          productName: 'SofÃ¡ Oslo',
          quantity: 1,
          unitPrice: 2499,
          subtotal: 2499
        }
      ];
      return jsonResponse({
        id: 'cart-1',
        customerId: '11111111-1111-1111-1111-111111111111',
        items: [
          {
            productId: 'prod-1',
            productName: 'Sofá Oslo',
            quantity: 1,
            unitPrice: 2499,
            subtotal: 2499
          }
        ],
        totalAmount: calculateCartTotal()
      });
    }

    if (/\/api\/cart\/[^/]+\/items$/.test(url) && method === 'DELETE') {
      cartItems = [];
      return jsonResponse({
        id: 'cart-1',
        customerId: '11111111-1111-1111-1111-111111111111',
        items: cartItems,
        totalAmount: 0
      });
    }

    if (url.includes('/api/cart/') && method === 'DELETE') {
      cartItems = [];
      return jsonResponse({ message: 'Item eliminado del carrito' });
    }

    if (url.includes('/api/cart/') && method === 'GET') {
      return jsonResponse({
        id: 'cart-1',
        customerId: '11111111-1111-1111-1111-111111111111',
        items: cartItems,
        totalAmount: calculateCartTotal()
      });
    }

    if (url.includes('/api/cart/')) {
      return jsonResponse({
        id: 'cart-1',
        customerId: '11111111-1111-1111-1111-111111111111',
        items: [
          {
            productId: 'prod-1',
            productName: 'Sofá Oslo',
            quantity: 1,
            unitPrice: 2499,
            subtotal: 2499
          }
        ],
        totalAmount: calculateCartTotal()
      });
    }

    return jsonResponse({});
  });
});

describe('App', () => {
  it('renders with login and shows admin menu for admin user', async () => {
    render(<App />);

    expect(screen.getByText('Tienda de muebles modernos')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }));

    expect(await screen.findByText('Menú')).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Órdenes' })).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Pagos' })).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Usuarios' })).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Carritos' })).toBeInTheDocument();
    expect(await screen.findByText('Rol: Admin')).toBeInTheDocument();
  });

  it('shows customer options and hides admin menu for customer user', async () => {
    loginRole = 'Customer';
    loginFullName = 'Cliente Final';
    loginEmail = 'final@muebles.com';

    render(<App />);

    fireEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }));

    expect(await screen.findByText('Rol: Customer')).toBeInTheDocument();
    expect(await screen.findByText('Opciones de cliente')).toBeInTheDocument();
    expect(await screen.findByText('Mis facturas')).toBeInTheDocument();
    expect(screen.queryByText('Menú')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Usuarios' })).not.toBeInTheDocument();
  });

  it('supports inventory crud from admin panel', async () => {
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }));
    await screen.findByText('Menú');

    fireEvent.click(screen.getByRole('button', { name: 'Inventario' }));
    expect(await screen.findByText('Sofá Oslo')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('SKU producto'), { target: { value: 'SKU-002' } });
    fireEvent.change(screen.getByLabelText('Nombre producto'), { target: { value: 'Mesa Nórdica' } });
    fireEvent.change(screen.getByLabelText('Categoría producto'), { target: { value: 'Comedor' } });
    fireEvent.change(screen.getByLabelText('Precio producto'), { target: { value: '1999' } });
    fireEvent.change(screen.getByLabelText('Proveedor producto'), { target: { value: 'Proveedor Dos' } });
    fireEvent.change(screen.getByLabelText('Stock disponible'), { target: { value: '8' } });
    fireEvent.change(screen.getByLabelText('Stock reservado'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Crear producto' }));

    expect(await screen.findByText('Producto creado correctamente')).toBeInTheDocument();
    expect(await screen.findByText('Mesa Nórdica')).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole('button', { name: 'Eliminar' })[0]);
    expect(await screen.findByText('Producto eliminado correctamente')).toBeInTheDocument();
  });

  it('supports orders and payments crud flows', async () => {
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }));
    await screen.findByText('Menú');

    fireEvent.click(screen.getByRole('button', { name: 'Órdenes' }));
    expect(await screen.findByText('order-1')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Producto orden'), { target: { value: 'prod-1' } });
    fireEvent.change(screen.getByLabelText('Precio unitario orden'), { target: { value: '2499' } });
    fireEvent.click(screen.getByRole('button', { name: 'Crear orden' }));
    expect(await screen.findByText('Orden creada correctamente')).toBeInTheDocument();
    expect(await screen.findByText('order-2')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Seleccionar orden'), { target: { value: 'order-1' } });
    fireEvent.change(screen.getByLabelText('Estado orden'), { target: { value: 'Cancelled' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar estado' }));
    expect(await screen.findByText('Orden actualizada correctamente')).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole('button', { name: 'Eliminar' })[0]);
    expect(await screen.findByText('Orden eliminada correctamente')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Pagos' }));
    expect(await screen.findByText('payment-1')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Orden pago'), { target: { value: 'order-1' } });
    fireEvent.change(screen.getByLabelText('Producto pago'), { target: { value: 'prod-1' } });
    fireEvent.change(screen.getByLabelText('Nombre producto pago'), { target: { value: 'Sofá Oslo' } });
    fireEvent.change(screen.getByLabelText('Precio pago'), { target: { value: '2499' } });
    fireEvent.click(screen.getByRole('button', { name: 'Autorizar pago' }));
    expect(await screen.findByText('Pago autorizado correctamente')).toBeInTheDocument();
    expect(await screen.findByText('payment-2')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Seleccionar pago'), { target: { value: 'payment-1' } });
    fireEvent.change(screen.getByLabelText('Estado pago'), { target: { value: 'Captured' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar pago' }));
    expect(await screen.findByText('Pago actualizado correctamente')).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole('button', { name: 'Eliminar' }).slice(-1)[0]);
    expect(await screen.findByText('Pago eliminado correctamente')).toBeInTheDocument();
  });

  it('blocks checkout for guest users and asks them to log in or register', async () => {
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: /agregar al carrito/i }));
    await screen.findByText(/agregado al carrito/i);

    fireEvent.click(screen.getByRole('button', { name: 'Pagar y generar factura' }));

    expect(await screen.findAllByText('Debes iniciar sesion o registrarte para poder realizar el pago.')).not.toHaveLength(0);
    expect(mockFetch.mock.calls.some(([input, init]) => String(input).includes('/api/orders') && init?.method === 'POST')).toBe(false);
    expect(mockFetch.mock.calls.some(([input, init]) => String(input).includes('/api/payments/authorize') && init?.method === 'POST')).toBe(false);
  });

  it('supports user crud and cart flow for admin', async () => {
    render(<App />);

    expect(await screen.findByText('Sofá Oslo')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /agregar al carrito/i }));
    await screen.findByText('Sofá Oslo agregado al carrito');

    fireEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }));
    await screen.findByText('Menú');

    fireEvent.click(screen.getByRole('button', { name: 'Usuarios' }));
    expect(await screen.findByText('Administrador')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Correo usuario'), { target: { value: 'nuevo@muebles.com' } });
    fireEvent.change(screen.getByLabelText('Nombre usuario'), { target: { value: 'Nuevo Usuario' } });
    fireEvent.change(screen.getByLabelText('Password usuario'), { target: { value: 'Password123!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Crear usuario' }));
    expect(await screen.findByText('Usuario creado correctamente')).toBeInTheDocument();
    expect(await screen.findByText('Nuevo Usuario')).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole('button', { name: 'Eliminar' }).slice(-1)[0]);
    expect(await screen.findByText('Usuario eliminado correctamente')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Carritos' }));
    expect(await screen.findByText('Consulta de carritos.')).toBeInTheDocument();
  });

  it('persists logged user and allows logout', async () => {
    render(<App />);

    fireEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }));

    expect(await screen.findByText('Rol: Admin')).toBeInTheDocument();
    expect(localStorage.getItem('muebles.session')).toContain('Cliente Demo');

    fireEvent.click(screen.getByRole('button', { name: 'Cerrar sesión' }));

    expect(await screen.findByText('Sesión actual: Cliente invitado')).toBeInTheDocument();
    expect(screen.getByText('Rol: Guest')).toBeInTheDocument();
    expect(localStorage.getItem('muebles.session')).toBeNull();
  });
});
