import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { CartPanel } from './CartPanel';

describe('CartPanel', () => {
  it('shows empty cart state and disables checkout', () => {
    const onCheckout = vi.fn();

    render(
      <CartPanel
        cart={{ id: 'cart-1', customerId: 'customer-1', items: [], totalAmount: 0 }}
        checkoutDisabled
        onCheckout={onCheckout}
      />
    );

    expect(screen.getByText(/carrito.*vac/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pagar y generar factura' })).toBeDisabled();
    expect(onCheckout).not.toHaveBeenCalled();
  });
});
