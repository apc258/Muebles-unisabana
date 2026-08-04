import { useState } from 'react';
import { api, sessionStorageService } from '../services/api';
import { validateRegistrationForm } from '../validation/formValidation';

interface LoginSuccessPayload {
  customerId: string;
  fullName: string;
  email: string;
  token: string;
  role: string;
}

interface Props {
  onLoginSuccess?: (payload: LoginSuccessPayload) => void;
}

type FormMode = 'login' | 'register' | 'forgot';

export function LoginForm({ onLoginSuccess }: Props) {
  const [mode, setMode] = useState<FormMode>('login');
  const [email, setEmail] = useState('cliente@muebles.com');
  const [password, setPassword] = useState('Password123!');
  const [registerFullName, setRegisterFullName] = useState('');
  const [registerIdentification, setRegisterIdentification] = useState('');
  const [registerEmail, setRegisterEmail] = useState('');
  const [registerPassword, setRegisterPassword] = useState('');
  const [registerPasswordConfirm, setRegisterPasswordConfirm] = useState('');
  const [showLoginPassword, setShowLoginPassword] = useState(false);
  const [showRegisterPassword, setShowRegisterPassword] = useState(false);
  const [forgotFullName, setForgotFullName] = useState('');
  const [forgotEmail, setForgotEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const changeMode = (nextMode: FormMode) => {
    setMode(nextMode);
    setMessage(null);
    setError(null);

    if (nextMode === 'forgot') {
      setForgotEmail(email);
    }
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLoading(true);
    setError(null);
    setMessage(null);

    try {
      const response = await api.login({ email, password });

      sessionStorageService.save({
        id: response.user.id!,
        email: response.user.email,
        fullName: response.user.fullName,
        role: response.user.role ?? 'Customer',
        token: response.token
      });

      setMessage(`Bienvenido ${response.user.fullName}`);
      onLoginSuccess?.({
        customerId: response.user.id ?? '',
        fullName: response.user.fullName,
        email: response.user.email,
        token: response.token,
        role: response.user.role ?? 'Customer'
      });
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : 'No fue posible iniciar sesión');
    } finally {
      setLoading(false);
    }
  };

  const handleRegister = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLoading(true);
    setError(null);
    setMessage(null);

    const validationErrors = validateRegistrationForm({
      fullName: registerFullName,
      identification: registerIdentification,
      email: registerEmail,
      password: registerPassword,
      passwordConfirm: registerPasswordConfirm
    });

    if (validationErrors.length > 0) {
      setLoading(false);
      setError(validationErrors.join(' '));
      return;
    }

    try {
      await api.createUser({
        fullName: registerFullName,
        identification: registerIdentification,
        email: registerEmail,
        password: registerPassword
      });

      setEmail(registerEmail);
      setPassword('');
      setRegisterFullName('');
      setRegisterIdentification('');
      setRegisterEmail('');
      setRegisterPassword('');
      setRegisterPasswordConfirm('');
      setShowRegisterPassword(false);
      setMode('login');
      setMessage('Registro creado. Ya puedes iniciar sesión.');
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : 'No fue posible registrar el usuario');
    } finally {
      setLoading(false);
    }
  };

  const handleForgotPassword = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLoading(true);
    setError(null);
    setMessage(null);

    try {
      const response = await api.forgotPassword({
        fullName: forgotFullName,
        email: forgotEmail
      });

      setMode('login');
      setMessage(response.message || 'Su operacion esta en progreso');
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : 'No fue posible enviar la solicitud');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-stone-200">
      <h3 className="mb-5 text-xl font-semibold">Autenticacion</h3>

      {mode === 'login' && (
        <form className="space-y-4" onSubmit={handleSubmit}>
          <input
            className="w-full rounded-xl border border-stone-300 px-4 py-3 text-sm"
            type="email"
            placeholder="correo@ejemplo.com"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
          />
          <div className="grid grid-cols-[minmax(0,1fr)_86px] gap-2">
            <input
              className="min-w-0 rounded-xl border border-stone-300 px-4 py-3 text-sm"
              type={showLoginPassword ? 'text' : 'password'}
              placeholder="********"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
            <button className="rounded-xl border border-stone-300 px-3 py-2 text-xs font-medium text-stone-700 hover:bg-stone-50" type="button" onClick={() => setShowLoginPassword((current) => !current)}>
              {showLoginPassword ? 'Ocultar' : 'Mostrar'}
            </button>
          </div>
          <button className="w-full rounded-xl bg-brand-700 px-4 py-3 font-medium text-white disabled:bg-brand-300" type="submit" disabled={loading}>
            {loading ? 'Iniciando sesión...' : 'Iniciar sesión'}
          </button>
          <div className="grid gap-2 sm:grid-cols-2">
            <button className="rounded-lg border border-stone-300 px-3 py-2 text-xs font-medium text-stone-700 hover:bg-stone-50" type="button" onClick={() => changeMode('register')}>
              Registro
            </button>
            <button className="rounded-lg border border-stone-300 px-3 py-2 text-xs font-medium text-stone-700 hover:bg-stone-50" type="button" onClick={() => changeMode('forgot')}>
              Olvidé contraseña
            </button>
          </div>
        </form>
      )}

      {mode === 'register' && (
        <form className="space-y-4" onSubmit={handleRegister}>
          <input
            className="w-full rounded-xl border border-stone-300 px-4 py-3 text-sm"
            type="text"
            placeholder="Nombre completo"
            value={registerFullName}
            onChange={(event) => setRegisterFullName(event.target.value)}
            required
          />
          <input
            className="w-full rounded-xl border border-stone-300 px-4 py-3 text-sm"
            type="text"
            placeholder="Identificacion"
            value={registerIdentification}
            onChange={(event) => setRegisterIdentification(event.target.value)}
            required
          />
          <input
            className="w-full rounded-xl border border-stone-300 px-4 py-3 text-sm"
            type="email"
            placeholder="correo@ejemplo.com"
            value={registerEmail}
            onChange={(event) => setRegisterEmail(event.target.value)}
            required
          />
          <div className="grid grid-cols-[minmax(0,1fr)_86px] gap-2">
            <input
              className="min-w-0 rounded-xl border border-stone-300 px-4 py-3 text-sm"
              type={showRegisterPassword ? 'text' : 'password'}
              placeholder="Password"
              value={registerPassword}
              onChange={(event) => setRegisterPassword(event.target.value)}
              required
            />
            <button className="rounded-xl border border-stone-300 px-3 py-2 text-xs font-medium text-stone-700 hover:bg-stone-50" type="button" onClick={() => setShowRegisterPassword((current) => !current)}>
              {showRegisterPassword ? 'Ocultar' : 'Mostrar'}
            </button>
          </div>
          <input
            className="w-full rounded-xl border border-stone-300 px-4 py-3 text-sm"
            type={showRegisterPassword ? 'text' : 'password'}
            placeholder="Confirmar password"
            value={registerPasswordConfirm}
            onChange={(event) => setRegisterPasswordConfirm(event.target.value)}
            required
          />
          <div className="flex gap-2">
            <button className="flex-1 rounded-xl bg-brand-700 px-4 py-3 font-medium text-white disabled:bg-brand-300" type="submit" disabled={loading}>
              {loading ? 'Registrando...' : 'Crear registro'}
            </button>
            <button className="rounded-xl border border-stone-300 px-4 py-3 text-sm font-medium text-stone-700" type="button" onClick={() => changeMode('login')}>
              Volver
            </button>
          </div>
        </form>
      )}

      {mode === 'forgot' && (
        <form className="space-y-4" onSubmit={handleForgotPassword}>
          <input
            className="w-full rounded-xl border border-stone-300 px-4 py-3 text-sm"
            type="text"
            placeholder="Nombre completo"
            value={forgotFullName}
            onChange={(event) => setForgotFullName(event.target.value)}
          />
          <input
            className="w-full rounded-xl border border-stone-300 px-4 py-3 text-sm"
            type="email"
            placeholder="correo@ejemplo.com"
            value={forgotEmail}
            onChange={(event) => setForgotEmail(event.target.value)}
            required
          />
          <div className="flex gap-2">
            <button className="flex-1 rounded-xl bg-brand-700 px-4 py-3 font-medium text-white disabled:bg-brand-300" type="submit" disabled={loading}>
              {loading ? 'Enviando...' : 'Enviar solicitud'}
            </button>
            <button className="rounded-xl border border-stone-300 px-4 py-3 text-sm font-medium text-stone-700" type="button" onClick={() => changeMode('login')}>
              Volver
            </button>
          </div>
        </form>
      )}

      {message && <p className="mt-3 text-sm text-emerald-600">{message}</p>}
      {error && <p className="mt-3 text-sm text-red-600">{error}</p>}
      <p className="mt-3 text-xs text-stone-500">Usuario demo sugerido: cliente@muebles.com / Password123!</p>
    </div>
  );
}
