const express = require('express');
const cors = require('cors');

const app = express();
const port = Number(process.env.PORT || 9090);

const services = {
  auth: process.env.AUTH_SERVICE_URL || 'http://authservice:8080',
  catalog: process.env.CATALOG_SERVICE_URL || 'http://catalogservice:8080',
  cart: process.env.CART_SERVICE_URL || 'http://cartservice:8080',
  orders: process.env.ORDER_SERVICE_URL || 'http://orderservice:8080',
  payments: process.env.PAYMENT_SERVICE_URL || 'http://paymentservice:8080',
  inventory: process.env.INVENTORY_SERVICE_URL || 'http://inventoryservice:8080'
};

const allowedOrigins = (process.env.CORS_ORIGIN || 'http://localhost:5173')
  .split(',')
  .map((origin) => origin.trim())
  .filter(Boolean);

app.use(cors({
  origin: (origin, callback) => {
    if (!origin || allowedOrigins.includes(origin)) {
      callback(null, true);
      return;
    }

    callback(new Error(`CORS blocked for origin: ${origin}`));
  },
  methods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'],
  allowedHeaders: ['Content-Type', 'Authorization', 'X-User-Id', 'X-User-Role'],
  credentials: true
}));

app.use(express.json());

app.get('/health', (_req, res) => {
  res.json({ status: 'ok', service: 'node-api-gateway', port, services });
});

async function proxyJson(req, res, baseUrl, path, init = {}) {
  try {
    const targetUrl = `${baseUrl}${path}`;
    const headers = {
      'Content-Type': 'application/json',
      'X-User-Id': req.headers['x-user-id'] || 'guest-user',
      'X-User-Role': req.headers['x-user-role'] || 'Customer',
      ...(init.headers || {})
    };

    const upstreamResponse = await fetch(targetUrl, {
      method: init.method || req.method,
      headers: headers,
      body: init.body
    });

    const text = await upstreamResponse.text();
    res.status(upstreamResponse.status);
    if (!text) return res.end();
    try { return res.json(JSON.parse(text)); } catch (_error) { return res.send(text); }
  } catch (error) {
    console.error(`Error de Gateway hacia ${baseUrl}${path}:`, error.message);
    return res.status(502).json({ message: 'Error de comunicación', detail: error.message });
  }
}

async function proxyBinary(req, res, baseUrl, path, init = {}) {
  try {
    const targetUrl = `${baseUrl}${path}`;
    const headers = {
      'X-User-Id': req.headers['x-user-id'] || 'guest-user',
      'X-User-Role': req.headers['x-user-role'] || 'Customer',
      ...(init.headers || {})
    };

    const upstreamResponse = await fetch(targetUrl, {
      method: init.method || req.method,
      headers
    });

    const contentType = upstreamResponse.headers.get('content-type');
    const contentDisposition = upstreamResponse.headers.get('content-disposition');
    const contentLength = upstreamResponse.headers.get('content-length');
    const body = Buffer.from(await upstreamResponse.arrayBuffer());

    res.status(upstreamResponse.status);
    if (contentType) res.setHeader('Content-Type', contentType);
    if (contentDisposition) res.setHeader('Content-Disposition', contentDisposition);
    if (contentLength) res.setHeader('Content-Length', contentLength);
    return res.send(body);
  } catch (error) {
    console.error(`Error de Gateway hacia ${baseUrl}${path}:`, error.message);
    return res.status(502).json({ message: 'Error de comunicaciÃ³n', detail: error.message });
  }
}

// --- RUTAS AUTENTICACIÓN ---
function requireAuthenticatedCustomer(req, res) {
  const userId = req.headers['x-user-id'];
  const userRole = req.headers['x-user-role'];

  if (!userId || !userRole || String(userRole).toLowerCase() === 'guest') {
    res.status(401).json({ message: 'Debes iniciar sesion o registrarte para poder realizar el pago.' });
    return false;
  }

  return true;
}

app.post('/api/auth/login', (req, res) => proxyJson(req, res, services.auth, '/api/auth/login', { method: 'POST', body: JSON.stringify(req.body) }));
app.post('/api/auth/register', (req, res) => proxyJson(req, res, services.auth, '/api/auth/register', { method: 'POST', body: JSON.stringify(req.body) }));
app.post('/api/auth/forgot-password', (req, res) => proxyJson(req, res, services.auth, '/api/auth/forgot-password', { method: 'POST', body: JSON.stringify(req.body) }));
app.get('/api/auth/users', (req, res) => proxyJson(req, res, services.auth, '/api/auth/users'));
app.put('/api/auth/users/:id', (req, res) => proxyJson(req, res, services.auth, `/api/auth/users/${req.params.id}`, { method: 'PUT', body: JSON.stringify(req.body) }));
app.delete('/api/auth/users/:id', (req, res) => proxyJson(req, res, services.auth, `/api/auth/users/${req.params.id}`, { method: 'DELETE' }));

// --- RUTAS CATÁLOGO ---
app.get('/api/catalog', (req, res) => proxyJson(req, res, services.catalog, '/api/catalog'));

// --- RUTAS CARRITO ---
app.get('/api/cart/:customerId', (req, res) => proxyJson(req, res, services.cart, `/api/cart/${req.params.customerId}`));
app.post('/api/cart/items', (req, res) => proxyJson(req, res, services.cart, '/api/cart/items', { method: 'POST', body: JSON.stringify(req.body) }));
app.delete('/api/cart/:customerId/items', (req, res) => proxyJson(req, res, services.cart, `/api/cart/${req.params.customerId}/items`, { method: 'DELETE' }));
app.delete('/api/cart/:customerId/items/:productId', (req, res) => proxyJson(req, res, services.cart, `/api/cart/${req.params.customerId}/items/${req.params.productId}`, { method: 'DELETE' }));

// --- RUTAS ÓRDENES ---
app.get('/api/orders', (req, res) => proxyJson(req, res, services.orders, '/api/orders'));
app.get('/api/orders/:orderId', (req, res) => proxyJson(req, res, services.orders, `/api/orders/${req.params.orderId}`));
app.post('/api/orders', (req, res) => proxyJson(req, res, services.orders, '/api/orders', { method: 'POST', body: JSON.stringify(req.body) }));
app.put('/api/orders/:orderId', (req, res) => proxyJson(req, res, services.orders, `/api/orders/${req.params.orderId}`, { method: 'PUT', body: JSON.stringify(req.body) }));
app.delete('/api/orders/:orderId', (req, res) => proxyJson(req, res, services.orders, `/api/orders/${req.params.orderId}`, { method: 'DELETE' }));

// --- RUTAS PAGOS ---
app.get('/api/payments', (req, res) => proxyJson(req, res, services.payments, '/api/payments'));
app.get('/api/payments/:paymentId', (req, res) => proxyJson(req, res, services.payments, `/api/payments/${req.params.paymentId}`));
app.get('/api/payments/:paymentId/invoice/pdf', (req, res) => proxyBinary(req, res, services.payments, `/api/payments/${req.params.paymentId}/invoice/pdf`));
app.post('/api/payments/authorize', (req, res) => {
  if (!requireAuthenticatedCustomer(req, res)) return;
  return proxyJson(req, res, services.payments, '/api/payments/authorize', { method: 'POST', body: JSON.stringify(req.body) });
});
app.put('/api/payments/:paymentId', (req, res) => proxyJson(req, res, services.payments, `/api/payments/${req.params.paymentId}`, { method: 'PUT', body: JSON.stringify(req.body) }));
app.delete('/api/payments/:paymentId', (req, res) => proxyJson(req, res, services.payments, `/api/payments/${req.params.paymentId}`, { method: 'DELETE' }));

// --- RUTAS INVENTARIO ---
app.get('/api/inventory/products', (req, res) => proxyJson(req, res, services.inventory, '/api/inventory/products'));
app.get('/api/inventory/products/:productId', (req, res) => proxyJson(req, res, services.inventory, `/api/inventory/products/${req.params.productId}`));
app.post('/api/inventory/products', (req, res) => proxyJson(req, res, services.inventory, '/api/inventory/products', { method: 'POST', body: JSON.stringify(req.body) }));
app.put('/api/inventory/products/:productId', (req, res) => proxyJson(req, res, services.inventory, `/api/inventory/products/${req.params.productId}`, { method: 'PUT', body: JSON.stringify(req.body) }));
app.delete('/api/inventory/products/:productId', (req, res) => proxyJson(req, res, services.inventory, `/api/inventory/products/${req.params.productId}`, { method: 'DELETE' }));

app.listen(port, () => {
  console.log(`Node API Gateway running on port ${port}`);
});
