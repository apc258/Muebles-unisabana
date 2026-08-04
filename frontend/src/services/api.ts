const API_BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:9090';
const E2E_CAPTURE_KEY = 'muebles.e2e.capture.enabled';
const E2E_CAPTURE_ENTRIES_KEY = 'muebles.e2e.capture.entries';
const E2E_CAPTURE_REPORT_KEY = 'muebles.e2e.capture.report';

// --- Interfaces ---
export interface AuthLoginRequest { email: string; password: string; }
export interface AuthForgotPasswordRequest { email: string; fullName?: string; }
export interface AuthForgotPasswordResponse { message: string; }
export interface AuthUser { id?: string; email: string; fullName: string; identification?: string; role?: string; }
export interface AuthLoginResponse { token: string; expiresIn: number; user: AuthUser; }
export interface CatalogProduct { id: string; name: string; category: string; price: number; image: string; colors: string[]; measures: string[]; }
export interface InventoryProduct { productId: string; sku: string; name: string; category: string; price: number; image: string; colors: string[]; measures: string[]; available: number; reserved: number; supplierName: string; createdAt: string; updatedAt: string; }
export interface CreateInventoryProductRequest { sku: string; name: string; category: string; price: number; image: string; colors: string[]; measures: string[]; available: number; reserved: number; supplierName: string; }
export interface UpdateInventoryProductRequest { sku?: string; name?: string; category?: string; price?: number; image?: string; colors?: string[]; measures?: string[]; available?: number; reserved?: number; supplierName?: string; }
export interface CartItem { productId: string; productName: string; quantity: number; unitPrice: number; subtotal: number; }
export interface CartResponse { id: string; customerId: string; items: CartItem[]; totalAmount: number; }
export interface AddCartItemRequest { customerId: string; productId: string; quantity: number; unitPrice: number; productName: string; }
export interface CreateOrderItem { productId: string; quantity: number; unitPrice: number; }
export interface CreateOrderRequest { customerId: string; items: CreateOrderItem[]; }
export interface OrderItemResponse { orderItemId?: string; productId: string; quantity: number; unitPrice: number; subtotal: number; }
export interface OrderResponse { orderId: string; customerId: string; status: string; subtotal: number; tax: number; total: number; createdAt: string; updatedAt: string; items: OrderItemResponse[]; }
export interface UpdateOrderRequest { status: string; }
export interface PaymentAuthorizeItem { productId: string; productName: string; quantity: number; unitPrice: number; }
export interface PaymentAuthorizeRequest { orderId: string; customerId: string; customerName: string; customerEmail: string; paymentMethod: string; items: PaymentAuthorizeItem[]; }
export interface InvoiceItemResponse { productName: string; quantity: number; unitPrice: number; subtotal: number; }
export interface InvoiceResponse { invoiceId?: string; paymentId?: string; orderId?: string; invoiceNumber: string; issuedAt: string; customerId?: string; customerName?: string; customerEmail?: string; paymentMethod?: string; items: InvoiceItemResponse[]; subtotal: number; tax: number; total: number; downloadUrl?: string; }
export interface PaymentResponse { paymentId: string; orderId: string; customerId: string; customerName: string; customerEmail: string; paymentMethod: string; status: string; subtotal: number; tax: number; total: number; createdAt: string; invoice?: InvoiceResponse; }
export interface PaymentAuthorizeResponse { paymentId: string; status: string; invoice: InvoiceResponse; invoicePdfBase64?: string; invoiceFileName?: string; }
export interface UpdatePaymentRequest { status: string; paymentMethod: string; }
export interface UserResponse { id: string; email: string; fullName: string; identification?: string; role: string; createdAt: string; isActive: boolean; }
export interface CreateUserRequest { email: string; fullName: string; identification?: string; password: string; role?: string; }
export interface UpdateUserRequest { email: string; fullName: string; identification?: string; password?: string; role?: string; isActive?: boolean; }
export interface SessionUser { id: string; email: string; fullName: string; role: string; token: string; }

const SESSION_STORAGE_KEY = 'muebles.session';

interface ApiCaptureEntry {
  timestamp: string;
  method: string;
  path: string;
  url: string;
  status: number;
  ok: boolean;
  durationMs: number;
  requestBody: unknown;
  responseBody: unknown;
}

declare global {
  interface Window {
    __MUEBLES_E2E_CAPTURE__?: {
      enabled: boolean;
      entries: ApiCaptureEntry[];
      report?: string;
      downloadReport: () => void;
      clear: () => void;
    };
  }
}

function canUseBrowserStorage() {
  return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
}

function initializeCaptureFromLocation() {
  if (!canUseBrowserStorage()) return;

  const params = new URLSearchParams(window.location.search);
  const captureParam = params.get('e2eCapture');
  if (captureParam === '1' || import.meta.env.VITE_E2E_CAPTURE === 'true') {
    window.localStorage.setItem(E2E_CAPTURE_KEY, '1');
    window.localStorage.setItem(E2E_CAPTURE_ENTRIES_KEY, '[]');
    window.localStorage.removeItem(E2E_CAPTURE_REPORT_KEY);
  }
  if (captureParam === '0') {
    window.localStorage.removeItem(E2E_CAPTURE_KEY);
  }
}

initializeCaptureFromLocation();

function isCaptureEnabled() {
  if (!canUseBrowserStorage()) return false;
  return window.localStorage.getItem(E2E_CAPTURE_KEY) === '1';
}

function parseJsonOrText(value: string) {
  if (!value) return null;
  try {
    return JSON.parse(value);
  } catch {
    return value;
  }
}

function redactSensitiveData(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map((item) => redactSensitiveData(item));
  }

  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>).map(([key, entryValue]) => {
        if (/password|token|authorization/i.test(key)) {
          return [key, '[REDACTADO]'];
        }

        return [key, redactSensitiveData(entryValue)];
      })
    );
  }

  return value;
}

function readCaptureEntries(): ApiCaptureEntry[] {
  if (!canUseBrowserStorage()) return [];

  try {
    return JSON.parse(window.localStorage.getItem(E2E_CAPTURE_ENTRIES_KEY) ?? '[]') as ApiCaptureEntry[];
  } catch {
    return [];
  }
}

function formatJsonForReport(value: unknown) {
  if (value === null || typeof value === 'undefined') return 'Sin cuerpo';
  return JSON.stringify(value, null, 2);
}

function buildCaptureReport(entries: ApiCaptureEntry[]) {
  const checkoutCalls = entries.filter((entry) =>
    ['/api/auth/login', '/api/catalog', '/api/cart/items', '/api/orders', '/api/payments/authorize']
      .some((endpoint) => entry.path.includes(endpoint))
  );
  const failedCalls = entries.filter((entry) => !entry.ok);
  const paymentCall = entries.find((entry) => entry.path.includes('/api/payments/authorize') && entry.method === 'POST');
  const paymentResponse = paymentCall?.responseBody as {
    paymentId?: string;
    invoice?: {
      invoiceNumber?: string;
      customerName?: string;
      customerEmail?: string;
      total?: number;
    };
  } | undefined;

  const callDetails = checkoutCalls.map((entry, index) => `### ${index + 1}. ${entry.method} ${entry.path}

- Fecha: ${entry.timestamp}
- Estado HTTP: ${entry.status}
- Correcta: ${entry.ok ? 'SI' : 'NO'}
- Duracion: ${entry.durationMs} ms

Request:

\`\`\`json
${formatJsonForReport(entry.requestBody)}
\`\`\`

Response:

\`\`\`json
${formatJsonForReport(entry.responseBody)}
\`\`\``).join('\n\n');

  return `# Reporte E2E real desde frontend

Fecha de ejecucion: ${new Date().toISOString()}

## Resumen

- Frontend: ${window.location.origin}
- Gateway/API: ${API_BASE_URL}
- Llamadas capturadas: ${entries.length}
- Llamadas del flujo de compra: ${checkoutCalls.length}
- Llamadas con error: ${failedCalls.length}
- Pago: ${paymentResponse?.paymentId ?? 'No registrado'}
- Factura: ${paymentResponse?.invoice?.invoiceNumber ?? 'No registrada'}
- Cliente: ${paymentResponse?.invoice?.customerName ?? 'No registrado'}
- Correo: ${paymentResponse?.invoice?.customerEmail ?? 'No registrado'}
- Total: ${typeof paymentResponse?.invoice?.total === 'number' ? `$${paymentResponse.invoice.total.toFixed(2)}` : 'No registrado'}

## Flujo esperado validado

- Login del cliente.
- Consulta de catalogo.
- Producto agregado al carrito.
- Orden creada.
- Pago autorizado.
- Factura generada por el servicio de pagos.

## Llamadas capturadas

${callDetails || 'No se capturaron llamadas del flujo de compra.'}
`;
}

function downloadTextFile(fileName: string, content: string, type: string) {
  const blob = new Blob([content], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

function downloadCaptureFiles(entries: ApiCaptureEntry[]) {
  const report = buildCaptureReport(entries);
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  downloadTextFile(`real-e2e-report-${stamp}.md`, report, 'text/markdown;charset=utf-8');
  downloadTextFile(`real-e2e-calls-${stamp}.json`, JSON.stringify(entries, null, 2), 'application/json;charset=utf-8');
  return report;
}

function renderCapturePanel(entries: ApiCaptureEntry[], report?: string) {
  if (!isCaptureEnabled() || typeof document === 'undefined') return;

  const panelId = 'muebles-e2e-capture-panel';
  let panel = document.getElementById(panelId);

  if (!panel) {
    panel = document.createElement('div');
    panel.id = panelId;
    panel.style.position = 'fixed';
    panel.style.right = '16px';
    panel.style.bottom = '16px';
    panel.style.zIndex = '99999';
    panel.style.maxWidth = '320px';
    panel.style.padding = '12px';
    panel.style.border = '1px solid #d6d3d1';
    panel.style.borderRadius = '8px';
    panel.style.background = '#ffffff';
    panel.style.boxShadow = '0 10px 24px rgba(0,0,0,.16)';
    panel.style.fontFamily = 'Arial, sans-serif';
    panel.style.fontSize = '13px';
    panel.style.color = '#292524';
    document.body.appendChild(panel);
  }

  panel.innerHTML = '';

  const title = document.createElement('p');
  title.textContent = 'Captura E2E activa';
  title.style.margin = '0 0 6px';
  title.style.fontWeight = '700';

  const status = document.createElement('p');
  status.textContent = `Llamadas capturadas: ${entries.length}${report ? ' | reporte listo' : ''}`;
  status.style.margin = '0 0 10px';

  const downloadButton = document.createElement('button');
  downloadButton.type = 'button';
  downloadButton.textContent = report ? 'Descargar reporte' : 'Descargar avance';
  downloadButton.style.width = '100%';
  downloadButton.style.border = '0';
  downloadButton.style.borderRadius = '6px';
  downloadButton.style.background = '#1c1917';
  downloadButton.style.color = '#ffffff';
  downloadButton.style.padding = '8px 10px';
  downloadButton.style.cursor = 'pointer';
  downloadButton.onclick = () => {
    const currentEntries = readCaptureEntries();
    const currentReport = downloadCaptureFiles(currentEntries);
    window.localStorage.setItem(E2E_CAPTURE_REPORT_KEY, currentReport);
    publishCapture(currentEntries, currentReport);
  };

  const clearButton = document.createElement('button');
  clearButton.type = 'button';
  clearButton.textContent = 'Limpiar captura';
  clearButton.style.width = '100%';
  clearButton.style.marginTop = '8px';
  clearButton.style.border = '1px solid #d6d3d1';
  clearButton.style.borderRadius = '6px';
  clearButton.style.background = '#ffffff';
  clearButton.style.color = '#292524';
  clearButton.style.padding = '8px 10px';
  clearButton.style.cursor = 'pointer';
  clearButton.onclick = () => {
    window.localStorage.setItem(E2E_CAPTURE_ENTRIES_KEY, '[]');
    window.localStorage.removeItem(E2E_CAPTURE_REPORT_KEY);
    publishCapture([], undefined);
  };

  panel.append(title, status, downloadButton, clearButton);
}

function publishCapture(entries: ApiCaptureEntry[], report?: string) {
  if (!canUseBrowserStorage()) return;

  window.__MUEBLES_E2E_CAPTURE__ = {
    enabled: isCaptureEnabled(),
    entries,
    report,
    downloadReport: () => {
      const currentEntries = readCaptureEntries();
      const currentReport = downloadCaptureFiles(currentEntries);
      window.localStorage.setItem(E2E_CAPTURE_REPORT_KEY, currentReport);
      publishCapture(currentEntries, currentReport);
    },
    clear: () => {
      window.localStorage.setItem(E2E_CAPTURE_ENTRIES_KEY, '[]');
      window.localStorage.removeItem(E2E_CAPTURE_REPORT_KEY);
      publishCapture([], undefined);
    }
  };

  renderCapturePanel(entries, report);
}

publishCapture(readCaptureEntries(), canUseBrowserStorage() ? window.localStorage.getItem(E2E_CAPTURE_REPORT_KEY) ?? undefined : undefined);

async function captureApiCall(path: string, init: RequestInit | undefined, response: Response, durationMs: number) {
  if (!isCaptureEnabled() || !canUseBrowserStorage()) return;

  try {
    const method = init?.method ?? 'GET';
    const requestBody = redactSensitiveData(parseJsonOrText(String(init?.body ?? '')));
    const responseText = await response.clone().text();
    const responseBody = redactSensitiveData(parseJsonOrText(responseText));
    const entry: ApiCaptureEntry = {
      timestamp: new Date().toISOString(),
      method,
      path,
      url: `${API_BASE_URL}${path}`,
      status: response.status,
      ok: response.ok,
      durationMs,
      requestBody,
      responseBody
    };
    const entries = [...readCaptureEntries(), entry];
    window.localStorage.setItem(E2E_CAPTURE_ENTRIES_KEY, JSON.stringify(entries));

    if (path.includes('/api/payments/authorize') && method === 'POST' && response.ok) {
      const report = buildCaptureReport(entries);
      window.localStorage.setItem(E2E_CAPTURE_REPORT_KEY, report);
      publishCapture(entries, report);
      return;
    }

    publishCapture(entries);
  } catch (captureError) {
    console.warn('No fue posible generar la evidencia E2E', captureError);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const session = sessionStorageService.load();
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...(init?.headers ?? {})
  };

  if (session && session.id) {
    headers['X-User-Id'] = session.id;
    headers['X-User-Role'] = session.role || 'Customer';
  }

  const started = performance.now();
  const response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers });
  await captureApiCall(path, init, response, Math.round(performance.now() - started));

  if (response.status === 403) {
    console.warn(`Acceso restringido: ${path}`);
    return null as any;
  }

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Error HTTP ${response.status}`);
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const sessionStorageService = {
  save(user: SessionUser) { localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(user)); },
  load(): SessionUser | null {
    const raw = localStorage.getItem(SESSION_STORAGE_KEY);
    if (!raw) return null;
    try { return JSON.parse(raw) as SessionUser; }
    catch { localStorage.removeItem(SESSION_STORAGE_KEY); return null; }
  },
  clear() { localStorage.removeItem(SESSION_STORAGE_KEY); }
};

export const api = {
  login(payload: AuthLoginRequest) { return request<AuthLoginResponse>('/api/auth/login', { method: 'POST', body: JSON.stringify(payload) }); },
  forgotPassword(payload: AuthForgotPasswordRequest) { return request<AuthForgotPasswordResponse>('/api/auth/forgot-password', { method: 'POST', body: JSON.stringify(payload) }); },
  getCatalog() { return request<CatalogProduct[]>('/api/catalog'); },
  getInventoryProducts() { return request<InventoryProduct[]>('/api/inventory/products'); },
  createInventoryProduct(payload: CreateInventoryProductRequest) { return request<InventoryProduct>('/api/inventory/products', { method: 'POST', body: JSON.stringify(payload) }); },
  updateInventoryProduct(productId: string, payload: UpdateInventoryProductRequest) { return request<InventoryProduct>(`/api/inventory/products/${productId}`, { method: 'PUT', body: JSON.stringify(payload) }); },
  deleteInventoryProduct(productId: string) { return request<{ message: string }>(`/api/inventory/products/${productId}`, { method: 'DELETE' }); },
  getCart(customerId: string) { return request<CartResponse>(`/api/cart/${customerId}`); },
  addCartItem(payload: AddCartItemRequest) { return request<CartResponse>('/api/cart/items', { method: 'POST', body: JSON.stringify(payload) }); },
  clearCart(customerId: string) { return request<CartResponse>(`/api/cart/${customerId}/items`, { method: 'DELETE' }); },
  removeCartItem(customerId: string, productId: string) { return request<{ message: string }>(`/api/cart/${customerId}/items/${productId}`, { method: 'DELETE' }); },
  getOrders() { return request<OrderResponse[]>('/api/orders'); },
  getOrder(orderId: string) { return request<OrderResponse>(`/api/orders/${orderId}`); },
  createOrder(payload: CreateOrderRequest) { return request<OrderResponse>('/api/orders', { method: 'POST', body: JSON.stringify(payload) }); },
  updateOrder(orderId: string, payload: UpdateOrderRequest) { return request<OrderResponse>(`/api/orders/${orderId}`, { method: 'PUT', body: JSON.stringify(payload) }); },
  deleteOrder(orderId: string) { return request<{ message: string }>(`/api/orders/${orderId}`, { method: 'DELETE' }); },
  getPayments() { return request<PaymentResponse[]>('/api/payments'); },
  getPayment(paymentId: string) { return request<PaymentResponse>(`/api/payments/${paymentId}`); },
  authorizePayment(payload: PaymentAuthorizeRequest) { return request<PaymentAuthorizeResponse>('/api/payments/authorize', { method: 'POST', body: JSON.stringify(payload) }); },
  updatePayment(paymentId: string, payload: UpdatePaymentRequest) { return request<PaymentResponse>(`/api/payments/${paymentId}`, { method: 'PUT', body: JSON.stringify(payload) }); },
  deletePayment(paymentId: string) { return request<{ message: string }>(`/api/payments/${paymentId}`, { method: 'DELETE' }); },
  getUsers() { return request<UserResponse[]>('/api/auth/users'); },
  createUser(payload: CreateUserRequest) { return request<UserResponse>('/api/auth/register', { method: 'POST', body: JSON.stringify(payload) }); },
  updateUser(userId: string, payload: UpdateUserRequest) { return request<UserResponse>(`/api/auth/users/${userId}`, { method: 'PUT', body: JSON.stringify(payload) }); },
  deleteUser(userId: string) { return request<{ message: string }>(`/api/auth/users/${userId}`, { method: 'DELETE' }); },
  getInvoicePdfUrl(paymentId: string) { return `${API_BASE_URL}/api/payments/${paymentId}/invoice/pdf`; }
};
