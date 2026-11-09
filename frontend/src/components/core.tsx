'use client';
import { BrandMark } from '@/components/brand-mark';
import { MotionDialog, MotionToast, MotionNumber } from '@/components/motion';
import { createContext, useContext, useEffect, useState, useCallback } from 'react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { ArrowRight, ShoppingBag, UserRound, X, Check, Utensils, LogOut, Menu } from 'lucide-react';
import { api, session, Session, Dish, Store } from '@/lib/api';
type Cart = { storeId: string; slug: string; storeName: string; items: { dish: Dish; quantity: number }[]; key: string };
type Context = { auth: Session | null; authLoading: boolean; refresh: () => Promise<void>; cart: Cart | null; cartScrollRequest: number; requestCartScroll: () => void; setCart: (cart: Cart | null) => void; add: (store: Store, dish: Dish) => void; notify: (s: string) => void; login: () => void; };
const Ctx = createContext<Context>(null!);
export const useApp = () => useContext(Ctx);
export function AppProvider({ children }: { children: React.ReactNode }) {
  const [auth, setAuth] = useState<Session | null>(null); const [authLoading, setAuthLoading] = useState(true);
  const [cart, updateCart] = useState<Cart | null>(null); const [hydrated, setHydrated] = useState(false);
  const [cartScrollRequest, setCartScrollRequest] = useState(0);
  const requestCartScroll = useCallback(() => setCartScrollRequest(value => value + 1), []);
  const [toast, setToast] = useState(''); const [toastVersion, setToastVersion] = useState(0);
  const notify = useCallback((message: string) => { setToast(message); setToastVersion(v => v + 1); }, []); const [showLogin, setShowLogin] = useState(false); const [busy, setBusy] = useState(false); const [loginError, setLoginError] = useState('');
  const refresh = useCallback(async () => { try { setAuth(await session()); } catch { setAuth(null); } finally { setAuthLoading(false); } }, []);
  useEffect(() => { void refresh(); try { const saved = localStorage.getItem('foodies.cart'); if (saved) { const c = JSON.parse(saved); if (c && typeof c.storeId === 'string' && Array.isArray(c.items) && c.items.every((i: Cart['items'][number]) => i.dish && Number.isInteger(i.quantity) && i.quantity > 0)) updateCart(c); } } catch {} setHydrated(true); }, [refresh]);
  useEffect(() => { if (hydrated) { if (cart) localStorage.setItem('foodies.cart', JSON.stringify(cart)); else localStorage.removeItem('foodies.cart'); } }, [cart, hydrated]);
  useEffect(() => { if (!toast) return; const t = setTimeout(() => setToast(''), 4500); return () => clearTimeout(t); }, [toast, toastVersion]);
  function setCart(c: Cart | null) { updateCart(c); }
  function add(store: Store, dish: Dish) {
    if (cart && cart.storeId !== store.id && !window.confirm(`Tu carrito tiene platillos de ${cart.storeName}. ¿Quieres vaciarlo y elegir en ${store.name}?`)) return;
    const current = cart?.storeId === store.id ? cart : { storeId: store.id, slug: store.slug, storeName: store.name, items: [], key: crypto.randomUUID() };
    const found = current.items.find(i => i.dish.id === dish.id);
    if (found && found.quantity >= 50) { notify('El máximo por platillo es 50.'); return; }
    const items = found ? current.items.map(i => i.dish.id === dish.id ? { dish, quantity: i.quantity + 1 } : i) : [...current.items, { dish, quantity: 1 }];
    setCart({ ...current, items, key: crypto.randomUUID() }); notify(`${dish.name} agregado`);
  }
  async function devLogin(account: string) { setBusy(true); setLoginError(''); try { await api(`/auth/development/${account}`, { method: 'POST' }); await refresh(); setShowLogin(false); } catch (e) { setLoginError((e as Error).message); } finally { setBusy(false); } }
  return <Ctx.Provider value={{ auth, authLoading, refresh, cart, cartScrollRequest, requestCartScroll, setCart, add, notify, login: () => { setLoginError(''); setShowLogin(true); } }}>{children}
    <MotionToast message={toast} version={toastVersion} onDismiss={() => setToast('')}/>
    <MotionDialog open={showLogin} onClose={() => setShowLogin(false)}><section className="modal" role="dialog" aria-modal="true" aria-labelledby="login-title" onClick={e => e.stopPropagation()}><button autoFocus className="icon close" aria-label="Cerrar" onClick={() => setShowLogin(false)}><X/></button><div className="modal-icon"><Utensils/></div><span className="eyebrow">YA CASI ES HORA DE COMER</span><h2 id="login-title">Tu próximo antojo<br/>empieza aquí.</h2><p>Inicia sesión para solicitar y seguir tus pedidos. Tu carrito te estará esperando.</p><a className={`button full ${!auth?.googleConfigured ? 'disabled' : ''}`} aria-disabled={!auth?.googleConfigured} href={auth?.googleConfigured ? `/api/v1/auth/google/start?returnUrl=${encodeURIComponent(window.location.pathname + window.location.search)}` : undefined}>Continuar con Google <ArrowRight size={18}/></a>{!auth?.googleConfigured && <p className="muted small">Google Login está pendiente de configuración.</p>}
    {auth?.developmentAccess && <div className="dev-access"><span>Entorno local · cuentas de prueba</span><div className="dev-grid">{[['customer','Cliente'],['manager','Encargada · Barrio'],['kitchen','Cocina · Barrio'],['manager-two','Encargada · Verde']].map(([id,label]) => <button key={id} disabled={busy} className="button secondary" onClick={() => devLogin(id)}>{label}</button>)}</div><p>Estas cuentas no verifican teléfonos ni envían SMS.</p></div>}{loginError && <ErrorBox text={loginError}/>}</section></MotionDialog>
  </Ctx.Provider>;
}
export function Header() {
  const { auth, cart, login, refresh, notify, requestCartScroll } = useApp(); const path = usePathname(); const router = useRouter(); const [open,setOpen] = useState(false);
  useEffect(() => setOpen(false), [path]);
  async function logout() { try { await api('/auth/logout', { method: 'POST' }); await refresh(); setOpen(false); } catch (e) { notify((e as Error).message); } }
  return <header><div className="header-inner">
    <Link href="/" className="brand" aria-label="Foodies inicio">foodies<span><BrandMark/></span></Link>
    <nav id="primary-nav" className={`motion-nav ${open ? 'is-open' : ''}`} aria-label="Navegación principal">
      <Link className={path === '/' || path.startsWith('/tiendas') ? 'active' : ''} href="/">Explorar tiendas</Link>
      <Link className={path.startsWith('/mis-pedidos') ? 'active' : ''} href="/mis-pedidos">Mis pedidos</Link>
      {!!auth?.memberships.length && <Link className={path.startsWith('/admin') ? 'active' : ''} href="/admin">Mi tienda</Link>}
      {auth?.user && <button type="button" className="mobile-logout" onClick={logout}><LogOut size={17} aria-hidden="true"/>Cerrar sesión</button>}
    </nav>
    <div className="header-actions">
      {cart && <Link aria-label="Ver carrito" className="cart-nav" href={`/tiendas/${cart.slug}#carrito`} onClick={event => {
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        event.preventDefault(); setOpen(false);
        const destination = `/tiendas/${encodeURIComponent(cart.slug.split('#')[0])}`;
        sessionStorage.setItem('foodies.cart-scroll', destination);
        requestCartScroll();
        // Navigate without a fragment: repeated hash pushes can accumulate it.
        if (window.location.pathname !== destination) router.push(destination, { scroll: false });
      }}><ShoppingBag size={19}/><b><MotionNumber value={cart.items.reduce((s,i) => s+i.quantity,0)}/></b></Link>}
      {auth?.user ? <><Link className="profile-link" href="/perfil" aria-label="Mi perfil"><UserRound size={18} aria-hidden="true"/><span>{auth.user.displayName.split(' ')[0]}</span></Link><button type="button" className="icon header-logout" aria-label="Cerrar sesión" onClick={logout}><LogOut size={17}/></button></> : <button className="button compact" onClick={login}><UserRound size={17}/><span>Iniciar sesión</span></button>}
      <button className="icon mobile-menu" aria-label={open ? 'Cerrar menú' : 'Abrir menú'} aria-controls="primary-nav" aria-expanded={open} onClick={() => setOpen(!open)}><span className="menu-icons" aria-hidden="true"><Menu/><X/></span></button>
    </div>
  </div></header>;
}
export function ErrorBox({ text, retry }: { text: string; retry?: () => void }) { return <div className="error-box" role="alert"><p>{text}</p>{retry && <button className="text-button" onClick={retry}>Volver a intentar <ArrowRight size={15}/></button>}</div>; }
export function Loading() { return <div className="loading" role="status"><span className="spinner"/>Preparando todo…</div>; }
export function AuthGate({ children }: { children: React.ReactNode }) { const { auth, authLoading, login } = useApp(); if (authLoading) return <Loading/>; if (!auth?.user) return <main className="page narrow"><div className="empty"><UserRound size={40}/><h1>Un lugar para tus pedidos.</h1><p>Inicia sesión para continuar.</p><button className="button" onClick={login}>Iniciar sesión <ArrowRight size={17}/></button></div></main>; return <>{children}</>; }
export function Steps({ current = 1 }: { current?: number }) { return <div className="steps">{['Elige tu tienda','Elige tus platillos','Solicita y recoge'].map((s,i) => <div className={i+1 === current ? 'current' : i+1 < current ? 'done' : ''} key={s}><b>{i+1 < current ? <Check size={13}/> : `0${i+1}`}</b><span>{s}</span>{i<2 && <span className="step-line"/>}</div>)}</div>; }
