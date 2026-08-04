import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LoginForm } from './LoginForm';

const mockFetch = vi.fn();
global.fetch = mockFetch as typeof fetch;

beforeEach(() => {
  mockFetch.mockReset();
  localStorage.clear();
});

describe('LoginForm registration validation', () => {
  it('does not call backend when name and identification are invalid', async () => {
    render(<LoginForm />);

    fireEvent.click(screen.getByRole('button', { name: 'Registro' }));
    fireEvent.change(screen.getByPlaceholderText('Nombre completo'), { target: { value: 'Cliente123 @' } });
    fireEvent.change(screen.getByPlaceholderText('Identificacion'), { target: { value: 'ABC12' } });
    fireEvent.change(screen.getByPlaceholderText('correo@ejemplo.com'), { target: { value: 'cliente@muebles.com' } });
    fireEvent.change(screen.getByPlaceholderText('Password'), { target: { value: 'Password123!' } });
    fireEvent.change(screen.getByPlaceholderText('Confirmar password'), { target: { value: 'Password123!' } });

    fireEvent.click(screen.getByRole('button', { name: 'Crear registro' }));

    expect(await screen.findByText(/El nombre completo solo debe contener letras y espacios/i)).toBeInTheDocument();
    expect(screen.getByText(/La identificacion debe tener entre 6 y 10 digitos numericos/i)).toBeInTheDocument();
    expect(mockFetch).not.toHaveBeenCalled();
  });
});
