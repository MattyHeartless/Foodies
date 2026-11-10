'use client';
import { BrandMark } from '@/components/brand-mark';
import { MotionTabs, MotionNumber, MotionPanel } from '@/components/motion';
import { use, useEffect, useState } from 'react';
import { StoreHours } from '@/components/store-hours';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { ArrowLeft, ArrowRight, Clock3, MapPin, Minus, Plus, ShoppingBag, ShieldCheck, X, Check } from 'lucide-react';
import { api, Store, Dish, Category, Order, money, ApiError } from '@/lib/api';
import { useApp, ErrorBox, Loading } from '@/components/core';
import { createCartKey } from '@/lib/cart-key';
export default function StorePage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = use(params); const router = useRouter(); const { auth, cart, setCart, add, login, cartScrollRequest } = useApp();
  const [store,setStore] = useState<Store | null>(null); const [dishes,setDishes] = useState<Dish[]>([]); const [categories,setCategories] = useState<Category[]>([]);
  const [category,setCategory] = useState('all'); const [error,setError] = useState(''); const [loading,setLoading] = useState(true); const [busy,setBusy] = useState(false); const [checkout,setCheckout] = useState(false); const [orderError,setOrderError] = useState('');
  async function load() { setError(''); try { const s = await api<Store>(`/stores/${slug}`); const menu = await api<{ dishes: Dish[]; categories: Category[] }>(`/stores/${s.id}/dishes`); setStore(s); setDishes(menu.dishes); setCategories(menu.categories); return menu; } catch(e) { setError((e as Error).message); } finally { setLoading(false); } }
  useEffect(() => { void load(); const t = setInterval(() => { void load(); },30000); return () => clearInterval(t); }, [slug]);
  useEffect(() => {
    if (loading || !store || store.slug !== slug) return;
    let cancelled = false;
    const scrollToCart = async () => {
      const destination = `/tiendas/${encodeURIComponent(slug)}`;
      const requested = sessionStorage.getItem('foodies.cart-scroll') === destination;
      const cartHash = window.location.hash.split('#').filter(Boolean)[0] === 'carrito';
      if (!requested && !cartHash) return;
      // Replace the entire fragment, including malformed legacy repetitions.
      window.history.replaceState(window.history.state, '', `${window.location.pathname}${window.location.search}#carrito`);
      // Let navigation finish before scrolling; Next's own hash restoration can
      // otherwise override a smooth scroll started during the route commit.
      await new Promise(resolve => setTimeout(resolve, 180));
      if (cancelled || window.location.hash !== '#carrito') return;
      await Promise.race([
        Promise.all(Array.from(document.querySelectorAll<HTMLImageElement>('.menu-page img')).map(image => image.decode().catch(() => {}))),
        new Promise(resolve => setTimeout(resolve, 1200)),
      ]);
      if (cancelled) return;
      document.querySelector('#carrito .cart-foot')?.scrollIntoView({
        behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth',
        block: 'end',
      });
      sessionStorage.removeItem('foodies.cart-scroll');
    };
    void scrollToCart();
    window.addEventListener('hashchange', scrollToCart);
    return () => { cancelled = true; window.removeEventListener('hashchange', scrollToCart); };
  }, [loading, slug, store?.id, cartScrollRequest]);
  const activeCart = cart?.storeId === store?.id ? cart : null; const total = activeCart?.items.reduce((s,i) => s + Math.round(i.dish.price*100)*i.quantity,0) || 0;
  const amount = total/100; const missing = Math.max(0,(store?.minimumOrderAmount || 0)-amount);
  function quantity(id: string, delta: number) { if (!activeCart) return; const items = activeCart.items.map(i => i.dish.id === id ? { ...i, quantity: Math.min(50,i.quantity+delta) } : i).filter(i => i.quantity > 0); setCart(items.length ? { ...activeCart, items, key: createCartKey() } : null); }
  async function submit() {
    if (!activeCart || !store) return;
    if (!auth?.user) { login(); return; }
    if (!auth.user.phoneE164 || (auth.phoneVerificationRequired && !auth.user.phoneVerifiedAt)) { router.push(`/perfil?returnTo=${encodeURIComponent(`/tiendas/${slug}#carrito`)}`); return; }
    setBusy(true); setOrderError('');
    try {
      const order = await api<Order>('/orders', { method: 'POST', headers: { 'Idempotency-Key': activeCart.key }, body: JSON.stringify({ storeId: store.id, items: activeCart.items.map(i => ({ dishId: i.dish.id, quantity: i.quantity, expectedUnitPrice: i.dish.price })) }) });
      setCart(null); router.push(`/mis-pedidos/${order.id}`);
    } catch(e) {
      setOrderError((e as Error).message);
      if (e instanceof ApiError && ['price_changed','unavailable','store_closed'].includes(e.code)) {
        const menu = await load();
        if (menu) { const items = activeCart.items.map(i => ({ ...i, dish: menu.dishes.find(d => d.id === i.dish.id) || { ...i.dish, isAvailable: false } })); setCart({ ...activeCart, items, key: createCartKey() }); }
      }
    } finally { setBusy(false); }
  }
  if (loading) return <Loading/>; if (!store) return <main className="page"><ErrorBox text={error} retry={load}/></main>;
  const unavailable = activeCart?.items.some(i => !dishes.some(d => d.id === i.dish.id && d.isAvailable));
  const currentPrices = activeCart?.items.some(i => { const d=dishes.find(d => d.id===i.dish.id); return d && d.price!==i.dish.price; });
  function reviewPrices() { if (!activeCart) return; setCart({ ...activeCart, key: createCartKey(), items: activeCart.items.map(i => ({ ...i, dish: dishes.find(d=>d.id===i.dish.id) || i.dish })) }); setOrderError('Precios actualizados. Revisa el total y confirma de nuevo.'); }
  return <main className="page menu-page"><Link className="back" href="/"><ArrowLeft size={16}/>Todas las tiendas</Link><div className="store-heading"><div><span className={`badge ${store.operationalState === 'Abierta' ? 'green' : 'gray'}`}><i/>{store.operationalState}</span><h1>{store.name}</h1><p>{store.description}</p><div className="inline-meta"><span><MapPin size={15}/>{store.addressText}</span><span><Clock3 size={15}/>{store.averagePreparationMinutes} min de preparación aprox.</span></div></div><div className="store-monogram">{store.slug.includes('verde') ? 'Vg' : 'Cb'}<span><BrandMark/></span></div></div>
    <div className="menu-layout"><section><MotionTabs className="category-tabs" activeKey={category} role="group" aria-label="Categorías del menú"><button className={category==='all'?'selected':''} onClick={()=>setCategory('all')}>Todo el menú</button>{categories.map(c=><button key={c.id} className={category===c.id?'selected':''} onClick={()=>setCategory(c.id)}>{c.name}</button>)}</MotionTabs>{error && <ErrorBox text={error}/>}{categories.filter(c=>category==='all'||category===c.id).map(c=><section className="dish-section" key={c.id}><h2>{c.name}<span>{dishes.filter(d=>d.categoryId===c.id).length}</span></h2><div className="dish-grid">{dishes.filter(d=>d.categoryId===c.id).map(d=><article className={`dish-card ${!d.isAvailable?'sold-out':''}`} key={d.id}><div className="dish-photo">{d.imageUrl ? <img src={d.imageUrl} alt={d.name}/> : <UtensilPlaceholder/>}{!d.isAvailable && <span className="badge gray">Agotado por hoy</span>}</div><div className="dish-body"><h3>{d.name}</h3><p>{d.description}</p><div><b>{money(d.price)}</b><button className="add-button" disabled={!d.isAvailable} aria-label={`Agregar ${d.name}`} onClick={()=>add(store,d)}><Plus size={19}/></button></div></div></article>)}</div></section>)}{!dishes.length && <div className="empty"><ShoppingBag/><h3>El menú está en preparación</h3><p>Vuelve pronto para ver los platillos de esta tienda.</p></div>}</section>
    <aside className="cart-panel" id="carrito"><div className="cart-title"><h2>Tu pedido</h2><ShoppingBag size={21}/></div>{!activeCart ? <div className="empty-cart"><ShoppingBag size={36}/><h3>Algo rico te espera.</h3><p>Agrega tus favoritos del menú para empezar.</p>{cart && <Link href={`/tiendas/${cart.slug}#carrito`}>Ver carrito de {cart.storeName}</Link>}</div> : <><div className="cart-items">{activeCart.items.map(i=><div className="cart-item" key={i.dish.id}><div><h4>{i.dish.name}</h4><span>{money(i.dish.price)}</span>{!dishes.some(d=>d.id===i.dish.id&&d.isAvailable)&&<small className="orange">Ya no está disponible</small>}</div><div className="quantity"><button aria-label={`Quitar una unidad de ${i.dish.name}`} onClick={()=>quantity(i.dish.id,-1)} disabled={busy}><Minus size={13}/></button><span><MotionNumber value={i.quantity}/></span><button aria-label={`Agregar una unidad de ${i.dish.name}`} onClick={()=>quantity(i.dish.id,1)} disabled={busy}><Plus size={13}/></button></div></div>)}</div><div className="cart-total"><span>Total</span><b><MotionNumber value={money(amount)}/> <small>MXN</small></b></div>{missing>0&&<p className="minimum">Te faltan {money(missing)} para el mínimo.</p>}{store.operationalState!=='Abierta'&&<p className="minimum">La tienda está {store.operationalState.toLowerCase()}. Puedes explorar y guardar tu carrito.</p>}{currentPrices&&<div className="error-box"><p>Hay precios nuevos. Revísalos antes de solicitar.</p><button className="text-button" onClick={reviewPrices}>Actualizar precios</button></div>}{orderError&&<ErrorBox text={orderError}/>}
      <MotionPanel open={checkout} className="checkout-summary"><span className="eyebrow">CONFIRMA TU SOLICITUD</span><p><Check size={15}/>Recoger y pagar en {store.name}</p><p><Clock3 size={15}/>La tienda debe aceptar tu pedido.</p><p><ShieldCheck size={15}/>{!auth?.user ? 'Necesitas iniciar sesión.' : !auth.user.phoneE164 ? 'Guarda tu teléfono para continuar.' : auth.phoneVerificationRequired && !auth.user.phoneVerifiedAt ? 'Verifica tu teléfono para continuar.' : auth.smsConfigured && auth.user.phoneVerifiedAt ? `Aviso SMS a ${auth.user.phoneE164}` : `Teléfono guardado: ${auth.user.phoneE164}. Consulta aquí el estado de tu pedido.`}</p></MotionPanel>
      <button className="button full" disabled={busy||missing>0||store.operationalState!=='Abierta'||unavailable||currentPrices} onClick={()=> checkout ? submit() : setCheckout(true)}>{busy?'Enviando solicitud…':!checkout?'Revisar mi pedido':!auth?.user?'Iniciar sesión para solicitar':!auth.user.phoneE164?'Guardar mi teléfono':auth.phoneVerificationRequired&&!auth.user.phoneVerifiedAt?'Verificar mi teléfono':'Confirmar solicitud'}<ArrowRight size={17}/></button>{checkout&&<button className="text-button full" disabled={busy} onClick={()=>setCheckout(false)}>Seguir eligiendo</button>}</>}
    <div className="cart-foot"><ShieldCheck size={17}/><span>Sin pago en línea.<br/>Pagas directamente al recoger.</span></div></aside></div><StoreHours store={store}/>
  </main>;
}
function UtensilPlaceholder() { return <div className="photo-placeholder"><BrandMark/></div>; }
