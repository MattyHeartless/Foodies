import { ApiError, type Store } from './api';

export type Administrator = { id: string; displayName: string; email: string };
export type PlatformSession = { csrfToken: string; administrator: Administrator | null };
export type Member = { id: string; storeId: string; userId: string; role: 'Manager' | 'Kitchen'; displayName: string; email: string; userActive: boolean };
export type PlatformStore = { store: Store; members: Member[] };
let csrf = '';

export async function platformApi<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response: Response;
  try { response = await fetch(`/api/v1/platform${path}`, { ...init, cache: 'no-store', headers: { ...(init.body instanceof FormData ? {} : { 'Content-Type': 'application/json' }), ...(csrf ? { 'X-CSRF-TOKEN': csrf } : {}), ...init.headers } }); }
  catch { throw new ApiError('network', 'No se pudo conectar con la plataforma. Revisa tu conexión e intenta de nuevo.', 0); }
  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'No se pudo conectar con la plataforma.' }));
    throw new ApiError(error.code || 'network', error.message || 'No se pudo completar la operación.', response.status);
  }
  if (response.status === 204) return undefined as T;
  return response.json();
}
export async function platformSession() {
  const state = await platformApi<PlatformSession>('/session'); csrf = state.csrfToken; return state;
}
