export interface RegistrationValidationInput {
  fullName: string;
  identification: string;
  email: string;
  password: string;
  passwordConfirm: string;
}

export interface InventoryValidationInput {
  sku: string;
  name: string;
  category: string;
  price: string;
  available: string;
  reserved: string;
  supplierName: string;
}

const personNamePattern = /^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]+(?:\s+[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]+)+$/;
const businessTextPattern = /^[A-Za-z0-9ÁÉÍÓÚÜÑáéíóúüñ .,-]+$/;
const identificationPattern = /^\d{6,10}$/;

export function validateRegistrationForm(form: RegistrationValidationInput) {
  const errors: string[] = [];
  const fullName = form.fullName.trim();
  const identification = form.identification.trim();

  if (!fullName) {
    errors.push('El nombre completo es obligatorio.');
  } else if (!personNamePattern.test(fullName)) {
    errors.push('El nombre completo solo debe contener letras y espacios.');
  }

  if (!identification) {
    errors.push('La identificacion es obligatoria.');
  } else if (!identificationPattern.test(identification)) {
    errors.push('La identificacion debe tener entre 6 y 10 digitos numericos.');
  }

  if (!form.email.trim()) {
    errors.push('El correo es obligatorio.');
  }

  if (!form.password.trim()) {
    errors.push('La contrasena es obligatoria.');
  }

  if (form.password !== form.passwordConfirm) {
    errors.push('Las contrasenas no coinciden.');
  }

  return errors;
}

export function validateInventoryForm(form: InventoryValidationInput) {
  const errors: string[] = [];
  const price = Number(form.price);
  const available = Number(form.available);
  const reserved = Number(form.reserved);
  const name = form.name.trim();
  const category = form.category.trim();
  const supplierName = form.supplierName.trim();

  if (!form.sku.trim()) errors.push('El SKU es obligatorio.');
  if (!name) errors.push('El nombre del producto es obligatorio.');
  if (name && !businessTextPattern.test(name)) errors.push('El nombre del producto contiene caracteres no permitidos.');
  if (!category) errors.push('La categoria es obligatoria.');
  if (category && !businessTextPattern.test(category)) errors.push('La categoria contiene caracteres no permitidos.');
  if (!supplierName) errors.push('El proveedor es obligatorio.');
  if (supplierName && !businessTextPattern.test(supplierName)) errors.push('El proveedor contiene caracteres no permitidos.');
  if (!form.price.trim() || Number.isNaN(price) || price <= 0) errors.push('El precio debe ser mayor que cero.');
  if (!form.available.trim() || Number.isNaN(available) || available < 0) errors.push('El stock disponible no puede ser negativo.');
  if (!form.reserved.trim() || Number.isNaN(reserved) || reserved < 0) errors.push('El stock reservado no puede ser negativo.');
  if (!Number.isNaN(available) && !Number.isNaN(reserved) && reserved > available) {
    errors.push('El stock reservado no puede superar el disponible.');
  }

  return errors;
}
