import { describe, expect, it } from 'vitest';
import { validateInventoryForm, validateRegistrationForm } from './formValidation';

describe('form validation rules', () => {
  it('accepts a valid registration form', () => {
    const errors = validateRegistrationForm({
      fullName: 'Carlos Gomez',
      identification: '1234567890',
      email: 'carlos@muebles.com',
      password: 'Password123!',
      passwordConfirm: 'Password123!'
    });

    expect(errors).toEqual([]);
  });

  it.each(['', 'Carlos123 Gomez', 'Carlos @ Gomez'])('rejects invalid customer full name: "%s"', (fullName) => {
    const errors = validateRegistrationForm({
      fullName,
      identification: '12345678',
      email: 'cliente@muebles.com',
      password: 'Password123!',
      passwordConfirm: 'Password123!'
    });

    expect(errors.some((error) => error.includes('nombre completo'))).toBe(true);
  });

  it.each(['abc123', '12345', '12345678901'])('rejects invalid identification: "%s"', (identification) => {
    const errors = validateRegistrationForm({
      fullName: 'Cliente Valido',
      identification,
      email: 'cliente@muebles.com',
      password: 'Password123!',
      passwordConfirm: 'Password123!'
    });

    expect(errors).toContain('La identificacion debe tener entre 6 y 10 digitos numericos.');
  });

  it('rejects registration when passwords do not match', () => {
    const errors = validateRegistrationForm({
      fullName: 'Cliente Valido',
      identification: '12345678',
      email: 'cliente@muebles.com',
      password: 'Password123!',
      passwordConfirm: 'Password456!'
    });

    expect(errors).toContain('Las contrasenas no coinciden.');
  });

  it('accepts a valid inventory form', () => {
    const errors = validateInventoryForm({
      sku: 'SOFA-001',
      name: 'Sofa 2 puestos',
      category: 'Sala',
      price: '2499',
      available: '10',
      reserved: '2',
      supplierName: 'Proveedor Uno'
    });

    expect(errors).toEqual([]);
  });

  it('rejects empty required inventory fields', () => {
    const errors = validateInventoryForm({
      sku: '',
      name: '',
      category: '',
      price: '',
      available: '',
      reserved: '',
      supplierName: ''
    });

    expect(errors).toEqual(expect.arrayContaining([
      'El SKU es obligatorio.',
      'El nombre del producto es obligatorio.',
      'La categoria es obligatoria.',
      'El proveedor es obligatorio.',
      'El precio debe ser mayor que cero.'
    ]));
  });

  it('rejects invalid inventory characters and stock boundaries', () => {
    const errors = validateInventoryForm({
      sku: 'MESA-001',
      name: 'Mesa @@@',
      category: 'Comedor #',
      price: '0',
      available: '1',
      reserved: '2',
      supplierName: 'Proveedor *'
    });

    expect(errors).toEqual(expect.arrayContaining([
      'El nombre del producto contiene caracteres no permitidos.',
      'La categoria contiene caracteres no permitidos.',
      'El proveedor contiene caracteres no permitidos.',
      'El precio debe ser mayor que cero.',
      'El stock reservado no puede superar el disponible.'
    ]));
  });
});
