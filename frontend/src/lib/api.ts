export type Hour = { dayOfWeek: number; openingTime: string; closingTime: string; isClosed: boolean };
export type Store = { coverImageUrl?: string | null; id: string; slug: string; name: string; description: string; addressText: string; operationalState: string; minimumOrderAmount: number; averagePreparationMinutes: number; timeZoneId: string; isActive: boolean; isPaused: boolean; hours: Hour[] };
export type Category = { id: string; storeId: string; name: string; sortOrder: number; isActive: boolean };
export type Dish = { id: string; storeId: string; categoryId: string; name: string; description: string; price: number; imageUrl?: string; isActive: boolean; isAvailable: boolean; sortOrder: number };
export type Order = { id: string; storeId: string; storeName?: string; publicCode: string; status: string; total: number; createdAt: string; acceptedAt?: string; estimatedPreparationSnapshotMin?: number; activeOrdersAheadSnapshot?: number; items: { dishId: string; nameSnapshot: string; unitPriceSnapshot: number; quantity: number; lineTotal: number }[]; history: { newStatus: string; previousStatus?: string; changedAt: string; reason?: string }[] };
export type Session = { csrfToken: string; user: null | { id: string; displayName: string; email: string; phoneE164?: string; phoneVerifiedAt?: string }; memberships: { storeId: string; role: string; name: string }[]; googleConfigured: boolean; smsConfigured: boolean; phoneVerificationRequired: boolean; developmentAccess: boolean };
let csrf = '';
export class ApiError extends Error { constructor(public code: string, message: string, public status: number) { super(message); } }
export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`/api/v1${path}`, { ...init, cache: 'no-store', headers: { ...(init.body instanceof FormData ? {} : { 'Content-Type': 'application/json' }), ...(csrf ? { 'X-CSRF-TOKEN': csrf } : {}), ...init.headers } });
  if (!response.ok) { const error = await response.json().catch(() => ({ message: 'No se pudo conectar con el servicio.' })); throw new ApiError(error.code || 'network', error.message || 'No se pudo completar la operación.', response.status); }
  if (response.status === 204) return undefined as T;
  return await response.json();
}
export async function session() { const s = await api<Session>('/auth/session'); csrf = s.csrfToken; return s; }
export const money = (n: number) => new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN', maximumFractionDigits: 2 }).format(n);
export const date = (s: string) => new Date(s).toLocaleString('es-MX', { dateStyle: 'medium', timeStyle: 'short' });
export const statusLabel: Record<string,string> = { Pendiente: 'Pendiente de aceptación', Aceptado: 'Aceptado', EnPreparacion: 'En preparación', Listo: 'Listo para recoger', Entregado: 'Entregado', Rechazado: 'Rechazado', Cancelado: 'Cancelado', NoRecogido: 'No recogido' };
