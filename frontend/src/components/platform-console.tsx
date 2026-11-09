'use client';
import { BrandMark } from './brand-mark';

import { useEffect, useRef, useState, type FormEvent } from 'react';
import Link from 'next/link';
import { ArrowLeft, ArrowRight, Check, ChevronRight, ExternalLink, LoaderCircle, LogOut, Menu, Plus, RefreshCw, Search, Settings2, ShieldCheck, Store as StoreIcon, Trash2, UserRound, X } from 'lucide-react';
import { StoreCoverField } from './store-cover-field';
import { ApiError, type Hour, type Store } from '@/lib/api';
import { platformApi, platformSession, type Administrator, type PlatformStore } from '@/lib/platform-api';
import styles from '@/app/plataforma/platform.module.css';

function Brand() { return <span className={styles.brand}><BrandMark/><strong>foodies</strong><span>Plataforma</span></span>; }
function Failure({ message }: { message: string }) { return message ? <p className={styles.error} role="alert">{message}</p> : null; }
function Busy({ label = 'Cargando' }: { label?: string }) { return <span className={styles.busy}><LoaderCircle size={16} aria-hidden="true"/>{label}</span>; }

export function PlatformConsole() {
  const [administrator, setAdministrator] = useState<Administrator | null>(null);
  const [checking, setChecking] = useState(true);
  const [sessionError, setSessionError] = useState('');
  async function check() {
    setChecking(true); setSessionError('');
    try { setAdministrator((await platformSession()).administrator); }
    catch (error) { setSessionError((error as Error).message); }
    finally { setChecking(false); }
  }
  useEffect(() => { void check(); }, []);
  return <div className={styles.root}>
    {checking ? <main className={styles.sessionLoading}><Brand/><h1>Administración de plataforma</h1><Busy label="Comprobando acceso"/></main>
      : sessionError ? <main className={styles.sessionLoading}><Brand/><h1>No se pudo abrir la plataforma</h1><Failure message={sessionError}/><button className={styles.primary} onClick={check}>Volver a intentar</button></main>
      : administrator ? <Workspace administrator={administrator} onExit={() => setAdministrator(null)}/>
      : <Login onLogin={setAdministrator}/>}
  </div>;
}

function Login({ onLogin }: { onLogin: (admin: Administrator) => void }) {
  const [email, setEmail] = useState(''); const [password, setPassword] = useState('');
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('');
    try {
      await platformSession();
      await platformApi('/login', { method: 'POST', body: JSON.stringify({ email, password }) });
      const state = await platformSession();
      if (!state.administrator) throw Error('No se pudo iniciar la sesión. Intenta de nuevo.');
      setPassword(''); onLogin(state.administrator);
    } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  return <main className={styles.login}>
    <div className={styles.loginBrand}><Brand/><Link href="/">Ir al sitio <ArrowRight size={16}/></Link></div>
    <div className={styles.loginGrid}>
      <section className={styles.loginContext}><div className={styles.contextTitle}><StoreIcon size={24}/><h2>La operación,<br/>en un solo lugar.</h2></div><p>Tiendas, horarios y responsables de Foodies.</p>
        <ul><li><Check size={17}/>Alta y configuración de tiendas</li><li><Check size={17}/>Encargados y personal de cocina</li><li><Check size={17}/>Publicación y pausa de pedidos</li></ul>
        <Link href="/admin">¿Administras una tienda? <ArrowRight size={16}/></Link>
      </section>
      <section className={styles.loginPanel}><ShieldCheck size={24} aria-hidden="true"/><h1>Acceso de administración</h1><p>Ingresa con tu cuenta de la plataforma.</p>
        <form onSubmit={submit}><label>Correo electrónico<input type="email" autoComplete="username" required maxLength={254} value={email} onChange={event => setEmail(event.target.value)}/></label>
          <label>Contraseña<input type="password" autoComplete="current-password" required maxLength={128} value={password} onChange={event => setPassword(event.target.value)}/></label>
          <Failure message={error}/><button className={styles.primary} disabled={busy}>{busy ? <Busy label="Ingresando"/> : <>Entrar a la plataforma<ArrowRight size={17}/></>}</button>
        </form><p className={styles.accessNote}>Acceso exclusivo para administradores de Foodies.</p>
      </section>
    </div>
  </main>;
}

type View = 'stores' | 'create' | 'edit' | 'security';
function Workspace({ administrator, onExit }: { administrator: Administrator; onExit: () => void }) {
  const [stores, setStores] = useState<PlatformStore[]>([]); const [loading, setLoading] = useState(true);
  const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [menu, setMenu] = useState(false);
  const [view, setView] = useState<View>('stores'); const [selected, setSelected] = useState('');
  const [search, setSearch] = useState(''); const [filter, setFilter] = useState('all');
  const [tab, setTab] = useState<'details' | 'members'>('details');
  const titleRef = useRef<HTMLHeadingElement>(null);
  async function load() {
    setLoading(true); setError('');
    try { setStores(await platformApi<PlatformStore[]>('/stores')); }
    catch (failure) { if (failure instanceof ApiError && failure.status === 401) onExit(); else setError((failure as Error).message); }
    finally { setLoading(false); }
  }
  useEffect(() => { void load(); }, []);
  useEffect(() => {
    if (!menu) return;
    const previous = document.querySelector<HTMLElement>('button[aria-label="Abrir navegación"]');
    const sidebar = document.querySelector<HTMLElement>('[data-platform-navigation]');
    const items = () => Array.from(sidebar?.querySelectorAll<HTMLElement>('button, a[href]') || []).filter(element => element.getClientRects().length > 0);
    const frame = requestAnimationFrame(() => items()[0]?.focus({ preventScroll: true }));
    const close = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setMenu(false);
      if (event.key !== 'Tab') return;
      const first = items()[0], last = items().at(-1);
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    };
    document.addEventListener('keydown', close);
    return () => { cancelAnimationFrame(frame); document.removeEventListener('keydown', close); previous?.focus({ preventScroll: true }); };
  }, [menu]);
  useEffect(() => { if (notice) { const timer = setTimeout(() => setNotice(''), 5000); return () => clearTimeout(timer); } }, [notice]);
  function navigate(next: View) { setView(next); setMenu(false); setError(''); setNotice(''); window.scrollTo({ top: 0, behavior: 'instant' }); requestAnimationFrame(() => titleRef.current?.focus()); }
  async function logout() {
    try { await platformApi('/logout', { method: 'POST' }); await platformSession(); onExit(); }
    catch (failure) { setError((failure as Error).message); }
  }
  const current = stores.find(row => row.store.id === selected);
  const matching = stores.filter(row => (filter === 'published' ? row.store.isActive : filter === 'draft' ? !row.store.isActive : true) && `${row.store.name} ${row.store.slug} ${row.store.addressText}`.toLocaleLowerCase('es-MX').includes(search.toLocaleLowerCase('es-MX')));
  const title = view === 'stores' ? 'Tiendas' : view === 'create' ? 'Nueva tienda' : view === 'security' ? 'Tu acceso' : current?.store.name || 'Tienda';
  return <div className={styles.workspace}>
    <aside data-platform-navigation className={`${styles.sidebar} ${menu ? styles.sidebarOpen : ''}`} role={menu ? 'dialog' : undefined} aria-modal={menu || undefined} aria-label="Administración de plataforma">
      <Brand/><button className={styles.mobileClose} onClick={() => setMenu(false)} aria-label="Cerrar navegación"><X size={20}/></button>
      <div className={styles.scope}>Administración general</div>
      <nav className={styles.sideNav} aria-label="Plataforma"><button aria-current={view !== 'security' ? 'page' : undefined} onClick={() => navigate('stores')}><StoreIcon size={17}/>Tiendas</button><button aria-current={view === 'security' ? 'page' : undefined} onClick={() => navigate('security')}><ShieldCheck size={17}/>Tu acceso</button></nav>
      <div className={styles.sidebarBottom}><Link href="/" target="_blank" rel="noopener noreferrer">Ver sitio público<ExternalLink size={15}/></Link><div className={styles.adminIdentity}><UserRound size={18}/><div><strong>{administrator.displayName}</strong><span>{administrator.email}</span></div></div><button onClick={logout}><LogOut size={17}/>Cerrar sesión</button></div>
    </aside>
    {menu && <button className={styles.menuBackdrop} aria-label="Cerrar navegación" onClick={() => setMenu(false)}/>}
    <div className={styles.topbar} inert={menu}><button className={styles.mobileMenu} aria-label="Abrir navegación" aria-expanded={menu} onClick={() => setMenu(true)}><Menu size={21}/></button><span>Foodies / <strong>{view === 'security' ? 'Tu acceso' : 'Tiendas'}</strong></span><span className={styles.topbarAccount}><ShieldCheck size={15}/>Administrador</span></div>
    <main className={styles.sheet} inert={menu}>
      <div className={styles.titleRow}><div>{(view === 'edit' || view === 'create') && <button className={styles.back} onClick={() => navigate('stores')}><ArrowLeft size={15}/>Tiendas</button>}<h1 ref={titleRef} tabIndex={-1}>{title}</h1><p>{view === 'stores' ? 'La red de tiendas y las personas que la hacen funcionar.' : view === 'create' ? 'Empieza con un borrador. Publícalo cuando esté listo.' : view === 'security' ? administrator.email : current ? `foodies / ${current.store.slug}` : ''}</p></div>
        {view === 'stores' && <button className={styles.primary} onClick={() => navigate('create')}><Plus size={17}/>Nueva tienda</button>}
      </div>
      <Failure message={error}/>{notice && <p className={styles.notice} role="status"><Check size={16}/>{notice}</p>}
      {view === 'stores' && <>
        <div className={styles.filters} role="group" aria-label="Filtrar tiendas">{[['all','Todas',stores.length],['published','Publicadas',stores.filter(row => row.store.isActive).length],['draft','Borradores',stores.filter(row => !row.store.isActive).length]].map(([key,label,count]) => <button key={key} aria-pressed={filter === key} onClick={() => setFilter(String(key))}><span>{label}</span><b>{count}</b></button>)}</div>
        <div className={styles.listTools}><label className={styles.search}><Search size={17}/><input type="search" aria-label="Buscar tiendas" placeholder="Buscar por nombre, enlace o dirección" value={search} onChange={event => setSearch(event.target.value)}/></label><button className={styles.secondary} disabled={loading} onClick={load} aria-label="Actualizar tiendas"><RefreshCw size={16}/><span>Actualizar</span></button></div>
        {loading ? <div className={styles.loadingRows} role="status"><Busy label="Cargando tiendas"/></div> : <div className={styles.storeList}>
          <div className={styles.listHead}><span>Tienda</span><span>Publicación</span><span>Responsables</span><span>Acción</span></div>
          {matching.map(row => <article className={styles.storeRow} key={row.store.id}><div className={styles.storeIdentity}><h2>{row.store.name}</h2><span>/{row.store.slug}</span><p>{row.store.addressText || 'Dirección pendiente'}</p></div>
            <div className={styles.storeState}><span className={`${styles.status} ${row.store.isActive ? row.store.isPaused ? styles.paused : styles.published : ''}`}>{!row.store.isActive ? 'Borrador' : row.store.isPaused ? 'En pausa' : 'Publicada'}</span><small>{row.store.hours.some(hour => !hour.isClosed) ? 'Horario configurado' : 'Horario pendiente'}</small></div>
            <div className={styles.teamSummary}><span>{row.members.filter(member => member.role === 'Manager' && member.userActive).length ? row.members.find(member => member.role === 'Manager' && member.userActive)?.displayName : 'Sin encargado'}</span><small>{row.members.length} {row.members.length === 1 ? 'persona asignada' : 'personas asignadas'}</small></div>
            <button className={styles.rowAction} aria-label={`Administrar ${row.store.name}`} onClick={() => { setSelected(row.store.id); setTab('details'); navigate('edit'); }}>Administrar<ChevronRight size={16}/></button>
          </article>)}
          {!matching.length && <div className={styles.empty}><h2>{stores.length ? 'Sin coincidencias' : 'Tu primera tienda'}</h2><p>{stores.length ? 'Prueba con otro nombre o cambia el filtro.' : 'Crea una tienda y asigna a la persona que la administrará.'}</p>{!stores.length && <button className={styles.primary} onClick={() => navigate('create')}><Plus size={16}/>Crear tienda</button>}</div>}
        </div>}
        <p className={styles.listFoot}>{matching.length} {matching.length === 1 ? 'tienda en esta vista' : 'tiendas en esta vista'}<span>Los borradores no aparecen en el catálogo público.</span></p>
      </>}
      {view === 'edit' && current && <><div className={styles.detailTabs} role="group" aria-label="Configuración de tienda"><button aria-pressed={tab === 'details'} onClick={() => setTab('details')}><Settings2 size={16}/>Datos y horarios</button><button aria-pressed={tab === 'members'} onClick={() => setTab('members')}><UserRound size={16}/>Responsables<span>{current.members.length}</span></button>{current.store.isActive && <a href={`/tiendas/${current.store.slug}`} target="_blank" rel="noopener noreferrer">Ver catálogo<ExternalLink size={14}/></a>}</div>
        {tab === 'details' ? <StoreForm key={current.store.id} initial={current.store} onCancel={() => navigate('stores')} onSaved={async () => { await load(); setNotice('Configuración guardada.'); }}/>
          : <Members row={current} onUpdated={async message => { await load(); setNotice(message); }}/>}</>}
      {view === 'create' && <StoreForm onCancel={() => navigate('stores')} onSaved={async store => { await load(); setSelected(store.id); setTab('members'); setView('edit'); setNotice('Tienda creada. Ya puedes asignar a su encargado.'); window.scrollTo({ top: 0, behavior: 'instant' }); }}/ >}
      {view === 'security' && <PasswordForm/>}
    </main>
  </div>;
}

type Settings = Omit<Store, 'id' | 'slug' | 'operationalState'>;
const fresh: Settings = { name: '', description: '', addressText: '', timeZoneId: 'America/Mexico_City', isActive: false, isPaused: true, minimumOrderAmount: 0, averagePreparationMinutes: 10, hours: [] };
const days = ['Domingo','Lunes','Martes','Miércoles','Jueves','Viernes','Sábado'];
function StoreForm({ initial, onSaved, onCancel }: { initial?: Store; onSaved: (store: Store) => Promise<void>; onCancel: () => void }) {
  const [form, setForm] = useState<Settings>(() => initial ? structuredClone(initial) : structuredClone(fresh));
  const [slug, setSlug] = useState(initial?.slug || ''); const [slugEdited, setSlugEdited] = useState(false);
  const [busy, setBusy] = useState(false); const [uploading,setUploading] = useState(false); const [error, setError] = useState('');
  function field<K extends keyof Settings>(name: K, value: Settings[K]) { setForm(previous => ({ ...previous, [name]: value })); }
  function hour(index: number, partial: Partial<Hour>) { field('hours', form.hours.map((value, i) => i === index ? { ...value, ...partial } : value)); }
  async function submit(event: FormEvent) {
    event.preventDefault(); if(uploading)return; setBusy(true); setError('');
    try { const saved = await platformApi<Store>(initial ? `/stores/${initial.id}` : '/stores', { method: initial ? 'PATCH' : 'POST', body: JSON.stringify(initial ? form : { slug, settings: form }) }); await onSaved(saved); }
    catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  return <form className={styles.storeForm} onSubmit={submit}>
    <section className={styles.formSection}><div className={styles.sectionLabel}><h2>Identidad de la tienda</h2><p>La información que verán tus clientes.</p></div><div className={styles.formPanel}>
      <label>Nombre<input required maxLength={100} value={form.name} onChange={event => { field('name', event.target.value); if (!initial && !slugEdited) setSlug(event.target.value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,'-').replace(/^-|-$/g,'')); }}/></label>
      <label>Enlace de la tienda<input required maxLength={80} pattern="[a-z0-9]+(-[a-z0-9]+)*" readOnly={!!initial} value={slug} onChange={event => { setSlugEdited(true); setSlug(event.target.value); }} aria-describedby="slug-help"/><small id="slug-help">/tiendas/{slug || 'nombre-de-la-tienda'}{initial ? ' · El enlace es permanente.' : ''}</small></label>
      <label>Descripción<textarea rows={3} maxLength={500} value={form.description} onChange={event => field('description', event.target.value)}/></label>
      <StoreCoverField storeId={initial?.id} platform onUploadingChange={setUploading} value={form.coverImageUrl} onChange={value=>field('coverImageUrl',value)}/>
      <label>Dirección de recogida<input maxLength={300} required={form.isActive} value={form.addressText} onChange={event => field('addressText', event.target.value)}/></label>
    </div></section>
    <section className={styles.formSection}><div className={styles.sectionLabel}><h2>Operación y publicación</h2><p>Las tiendas ocultas conservan su catálogo y sus pedidos.</p></div><div className={styles.formPanel}>
      <div className={styles.fieldPair}><label>Pedido mínimo · MXN<input type="number" min="0" max="100000" step="0.01" required value={form.minimumOrderAmount} onChange={event => field('minimumOrderAmount', Number(event.target.value))}/></label><label>Preparación · minutos<input type="number" min="1" max="240" required value={form.averagePreparationMinutes} onChange={event => field('averagePreparationMinutes', Number(event.target.value))}/></label></div>
      <label>Zona horaria<input required maxLength={100} value={form.timeZoneId} onChange={event => field('timeZoneId', event.target.value)} list="platform-timezones"/><datalist id="platform-timezones"><option value="America/Mexico_City"/><option value="America/Cancun"/><option value="America/Tijuana"/><option value="America/Hermosillo"/></datalist></label>
      <label className={styles.checkRow}><input type="checkbox" checked={form.isActive} onChange={event => field('isActive', event.target.checked)}/><span><strong>Publicar en el catálogo</strong><small>Requiere dirección y horario de apertura.</small></span></label>
      <label className={styles.checkRow}><input type="checkbox" checked={form.isPaused} onChange={event => field('isPaused', event.target.checked)}/><span><strong>Pausar nuevos pedidos</strong><small>La tienda publicada sigue visible mientras está en pausa.</small></span></label>
    </div></section>
    <section className={styles.formSection}><div className={styles.sectionLabel}><h2>Horario habitual</h2><p>Agrega un horario por día. Puedes agregar más de uno para jornadas partidas.</p><button type="button" className={styles.secondary} disabled={form.hours.length > 0} onClick={() => field('hours', [1,2,3,4,5,6,0].map(dayOfWeek => ({ dayOfWeek, openingTime: '08:00:00', closingTime: '20:00:00', isClosed: false })))}>Lun–Dom · 08:00–20:00</button></div>
      <div className={styles.formPanel}>{!form.hours.length && <p className={styles.inlineEmpty}>Sin horarios. Agrega uno antes de publicar.</p>}
        {form.hours.map((value, index) => <div className={styles.hourRow} key={index}><label>Día<select aria-label={`Día del horario ${index + 1}`} value={value.dayOfWeek} onChange={event => hour(index, { dayOfWeek: Number(event.target.value) })}>{days.map((day, i) => <option value={i} key={day}>{day}</option>)}</select></label><label>Apertura<input type="time" required disabled={value.isClosed} value={value.openingTime.slice(0,5)} onChange={event => hour(index, { openingTime: event.target.value + ':00' })}/></label><label>Cierre<input type="time" required disabled={value.isClosed} value={value.closingTime.slice(0,5)} onChange={event => hour(index, { closingTime: event.target.value + ':00' })}/></label><label className={styles.closedDay}><input type="checkbox" checked={value.isClosed} onChange={event => hour(index, { isClosed: event.target.checked })}/>Cerrado</label><button type="button" className={styles.iconButton} aria-label={`Eliminar horario ${index + 1}`} onClick={() => field('hours', form.hours.filter((_,i) => i !== index))}><Trash2 size={16}/></button></div>)}
        <button className={styles.secondary} type="button" disabled={form.hours.length >= 28} onClick={() => field('hours', [...form.hours, { dayOfWeek: 1, openingTime: '08:00:00', closingTime: '20:00:00', isClosed: false }])}><Plus size={16}/>Agregar horario</button><small>Si el cierre es anterior a la apertura, termina al día siguiente.</small>
      </div></section>
    <div className={styles.saveBar}><Failure message={error}/><div><button type="button" className={styles.secondary} disabled={busy||uploading} onClick={onCancel}>Cancelar</button><button className={styles.primary} disabled={busy||uploading}>{busy ? <Busy label="Guardando"/> : initial ? 'Guardar cambios' : 'Crear tienda'}</button></div></div>
  </form>;
}

function Members({ row, onUpdated }: { row: PlatformStore; onUpdated: (message: string) => Promise<void> }) {
  const [email, setEmail] = useState(''); const [role, setRole] = useState('Manager');
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  async function assign(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('');
    try { await platformApi(`/stores/${row.store.id}/members`, { method: 'PUT', body: JSON.stringify({ email, role }) }); setEmail(''); await onUpdated('Responsable asignado.'); }
    catch (failure) { setError((failure as Error).message); } finally { setBusy(false); }
  }
  async function remove(id: string, name: string) {
    if (!window.confirm(`¿Retirar el acceso de ${name} a esta tienda?`)) return;
    setBusy(true); setError('');
    try { await platformApi(`/stores/${row.store.id}/members/${id}`, { method: 'DELETE' }); await onUpdated('Acceso retirado.'); }
    catch (failure) { setError((failure as Error).message); } finally { setBusy(false); }
  }
  return <div className={styles.membersLayout}><section><h2>Personas asignadas</h2><div className={styles.memberList}>
    {!row.members.length && <p className={styles.inlineEmpty}>Esta tienda todavía no tiene responsables.</p>}
    {row.members.map(member => <div className={styles.memberRow} key={member.id}><UserRound size={18}/><div><strong>{member.displayName}</strong><span>{member.email}</span><small>{member.role === 'Manager' ? 'Encargado' : 'Cocina'}{!member.userActive ? ' · Cuenta inactiva' : ''}</small></div><button type="button" className={styles.iconButton} aria-label={`Retirar acceso de ${member.displayName}`} disabled={busy} onClick={() => remove(member.id, member.displayName)}><Trash2 size={16}/></button></div>)}
  </div></section><section className={styles.formPanel}><h2>Asignar responsable</h2><p>La persona debe haber iniciado sesión con Google en Foodies. Si ya está asignada, se actualiza su rol.</p><form onSubmit={assign}><label>Correo de Google<input type="email" required maxLength={254} autoComplete="off" value={email} onChange={event => setEmail(event.target.value)}/></label><label>Rol<select value={role} onChange={event => setRole(event.target.value)}><option value="Manager">Encargado · tienda, catálogo y pedidos</option><option value="Kitchen">Cocina · pedidos y catálogo</option></select></label><Failure message={error}/><button className={styles.primary} disabled={busy}>{busy ? <Busy label="Asignando"/> : <><Plus size={16}/>Asignar responsable</>}</button></form></section></div>;
}

function PasswordForm() {
  const [currentPassword, setCurrent] = useState(''); const [newPassword, setNew] = useState(''); const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [success, setSuccess] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault(); setError(''); setSuccess(false);
    if (newPassword !== confirmation) { setError('Las contraseñas nuevas no coinciden.'); return; }
    setBusy(true);
    try { await platformApi('/password', { method: 'POST', body: JSON.stringify({ currentPassword, newPassword }) }); await platformSession(); setCurrent(''); setNew(''); setConfirmation(''); setSuccess(true); }
    catch (failure) { setError((failure as Error).message); } finally { setBusy(false); }
  }
  return <form className={styles.storeForm} onSubmit={submit}><section className={styles.formSection}><div className={styles.sectionLabel}><h2>Cambiar contraseña</h2><p>Usa entre 12 y 128 caracteres. El cambio cierra las otras sesiones de administración.</p></div><div className={styles.formPanel}><label>Contraseña actual<input type="password" required maxLength={128} autoComplete="current-password" value={currentPassword} onChange={event => setCurrent(event.target.value)}/></label><label>Nueva contraseña<input type="password" required minLength={12} maxLength={128} autoComplete="new-password" value={newPassword} onChange={event => setNew(event.target.value)} aria-describedby="password-help"/><small id="password-help">Entre 12 y 128 caracteres.</small></label><label>Confirmar nueva contraseña<input type="password" required minLength={12} maxLength={128} autoComplete="new-password" value={confirmation} onChange={event => setConfirmation(event.target.value)}/></label><Failure message={error}/>{success && <p className={styles.notice} role="status"><Check size={16}/>Contraseña actualizada.</p>}<button className={styles.primary} disabled={busy}>{busy ? <Busy label="Actualizando"/> : 'Actualizar contraseña'}</button></div></section></form>;
}
