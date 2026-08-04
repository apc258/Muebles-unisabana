import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api, sessionStorageService } from './api';

const mockFetch = vi.fn();
global.fetch = mockFetch as typeof fetch;

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), { status }));
}

beforeEach(() => {
  mockFetch.mockReset();
  localStorage.clear();
});

describe('api service connection routes', () => {
  it('connects to CatalogService through the gateway', async () => {
    mockFetch.mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200 }));

    await api.getCatalog();

    expect(mockFetch).toHaveBeenCalledWith(
      'http://localhost:9090/api/catalog',
      expect.objectContaining({
        headers: expect.objectContaining({
          'Content-Type': 'application/json'
        })
      })
    );
  });

  it('connects to AuthService login through the gateway', async () => {
    mockFetch.mockResolvedValueOnce(jsonResponse({
      token: 'token-demo',
      expiresIn: 3600,
      user: {
        id: '11111111-1111-1111-1111-111111111111',
        email: 'cliente@muebles.com',
        fullName: 'Cliente Demo',
        role: 'Customer'
      }
    }));

    await api.login({ email: 'cliente@muebles.com', password: 'Password123!' });

    expect(mockFetch).toHaveBeenCalledWith(
      'http://localhost:9090/api/auth/login',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ email: 'cliente@muebles.com', password: 'Password123!' })
      })
    );
  });

  it('sends authenticated customer headers to CartService through the gateway', async () => {
    sessionStorageService.save({
      id: '22222222-2222-2222-2222-222222222222',
      email: 'cliente@muebles.com',
      fullName: 'Cliente Demo',
      role: 'Customer',
      token: 'token-demo'
    });
    mockFetch.mockResolvedValueOnce(jsonResponse({
      id: 'cart-1',
      customerId: '22222222-2222-2222-2222-222222222222',
      items: [],
      totalAmount: 0
    }));

    await api.getCart('22222222-2222-2222-2222-222222222222');

    expect(mockFetch).toHaveBeenCalledWith(
      'http://localhost:9090/api/cart/22222222-2222-2222-2222-222222222222',
      expect.objectContaining({
        headers: expect.objectContaining({
          'X-User-Id': '22222222-2222-2222-2222-222222222222',
          'X-User-Role': 'Customer'
        })
      })
    );
  });

  it('connects to OrderService through the gateway with checkout payload', async () => {
    mockFetch.mockResolvedValueOnce(jsonResponse({
      orderId: 'order-1',
      customerId: '22222222-2222-2222-2222-222222222222',
      status: 'Created',
      subtotal: 2499,
      tax: 399.84,
      total: 2898.84,
      createdAt: '2026-06-15T00:00:00Z',
      updatedAt: '2026-06-15T00:00:00Z',
      items: []
    }, 201));

    await api.createOrder({
      customerId: '22222222-2222-2222-2222-222222222222',
      items: [{ productId: '33333333-3333-3333-3333-333333333333', quantity: 1, unitPrice: 2499 }]
    });

    expect(mockFetch).toHaveBeenCalledWith(
      'http://localhost:9090/api/orders',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          customerId: '22222222-2222-2222-2222-222222222222',
          items: [{ productId: '33333333-3333-3333-3333-333333333333', quantity: 1, unitPrice: 2499 }]
        })
      })
    );
  });

  it('connects to PaymentService through the gateway to authorize payment', async () => {
    mockFetch.mockResolvedValueOnce(jsonResponse({
      paymentId: 'payment-1',
      status: 'Authorized',
      invoice: {
        invoiceNumber: 'FAC-TEST-001',
        issuedAt: '2026-06-15T00:00:00Z',
        items: [],
        subtotal: 2499,
        tax: 399.84,
        total: 2898.84
      }
    }));

    await api.authorizePayment({
      orderId: '44444444-4444-4444-4444-444444444444',
      customerId: '22222222-2222-2222-2222-222222222222',
      customerName: 'Cliente Demo',
      customerEmail: 'cliente@muebles.com',
      paymentMethod: 'Tarjeta',
      items: [{ productId: '33333333-3333-3333-3333-333333333333', productName: 'Sofa Oslo', quantity: 1, unitPrice: 2499 }]
    });

    expect(mockFetch).toHaveBeenCalledWith(
      'http://localhost:9090/api/payments/authorize',
      expect.objectContaining({
        method: 'POST'
      })
    );
  });

  it('connects to InventoryService through the gateway', async () => {
    mockFetch.mockResolvedValueOnce(jsonResponse([]));

    await api.getInventoryProducts();

    expect(mockFetch).toHaveBeenCalledWith(
      'http://localhost:9090/api/inventory/products',
      expect.any(Object)
    );
  });
});
