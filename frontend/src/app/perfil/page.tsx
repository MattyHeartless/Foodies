'use client';

import { useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { ArrowLeft, ArrowRight, Check, CheckCheck, ChevronDown, Clock3, Mail, MessageSquare, Pencil, ShieldCheck } from 'lucide-react';
import { api } from '@/lib/api';
import { AuthGate, useApp } from '@/components/core';
import styles from './profile.module.css';

function localNumber(value = '') {
  const digits = value.replace(/\D/g, '');
  return digits.length === 12 && digits.startsWith('52') ? digits.slice(2) : digits;
}
export default function Profile() { return <AuthGate><ProfileContent/></AuthGate>; }
function ProfileContent() {
  const { auth, refresh, notify } = useApp();
  const user = auth!.user!;
  const [name, setName] = useState(user.displayName);
  const [phone, setPhone] = useState(() => user.phoneE164?.startsWith('+52') ? localNumber(user.phoneE164) : '');
  const [code, setCode] = useState(''); const [sentTo, setSentTo] = useState('');
  const [editingPhone, setEditingPhone] = useState(false);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [desktop, setDesktop] = useState(false);
  useEffect(() => {
    const media = window.matchMedia('(min-width: 901px)');
    const update = () => setDesktop(media.matches);
    update(); media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, []);
  const detailsExpanded = desktop || detailsOpen;
  const [busy, setBusy] = useState<'name' | 'send' | 'confirm' | null>(null);
  const [nameError, setNameError] = useState(''); const [phoneError, setPhoneError] = useState('');
  const [pageError, setPageError] = useState(''); const [savedName, setSavedName] = useState(false);
  const [returnTo, setReturnTo] = useState('/'); const [cooldown, setCooldown] = useState(0);
  const pinInput = useRef<HTMLInputElement>(null); const phoneInput = useRef<HTMLInputElement>(null);
  useEffect(() => {
    const parameters = new URLSearchParams(window.location.search);
    const destination = parameters.get('returnTo');
    if (destination?.startsWith('/') && !destination.startsWith('//') && !destination.includes('\\') && !/\p{Cc}/u.test(destination)) setReturnTo(destination);
    if (parameters.has('authError')) setPageError('No se pudo completar el acceso con Google. Intenta de nuevo.');
  }, []);
  useEffect(() => { if (!cooldown) return; const timer = setTimeout(() => setCooldown(value => value - 1), 1000); return () => clearTimeout(timer); }, [cooldown]);
  useEffect(() => { if (sentTo) pinInput.current?.focus(); }, [sentTo]);
  const verified = !!user.phoneVerifiedAt; const editing = !verified || editingPhone;
  const initials = user.displayName.trim().split(/\s+/).slice(0,2).map(part => part[0]).join('').toLocaleUpperCase('es-MX');
  async function saveName() {
    setBusy('name'); setNameError(''); setSavedName(false);
    try { await api('/me', { method:'PATCH', body:JSON.stringify({ displayName:name.trim() }) }); await refresh(); setSavedName(true); }
    catch (error) { setNameError((error as Error).message); } finally { setBusy(null); }
  }
  async function sendCode() {
    setPhoneError('');
    if (!/^\d{10}$/.test(phone)) { setPhoneError('Escribe los 10 dígitos de tu celular.'); phoneInput.current?.focus(); return; }
    setBusy('send');
    try {
      const phoneE164 = `+52${phone}`;
      if (!auth?.phoneVerificationRequired) {
        await api('/me/phone', { method:'PATCH', body:JSON.stringify({ phoneE164 }) });
        await refresh(); notify('Número guardado sin verificar.');
      } else {
        await api('/me/phone-verifications', { method:'POST', body:JSON.stringify({ phoneE164 }) });
        setCode(''); setSentTo(phoneE164); setCooldown(60);
      }
    } catch (error) { setPhoneError((error as Error).message); } finally { setBusy(null); }
  }
  async function confirmCode() {
    setBusy('confirm'); setPhoneError('');
    try {
      await api('/me/phone-verifications/confirm', { method:'POST', body:JSON.stringify({ code }) });
      await refresh(); setSentTo(''); setCode(''); setEditingPhone(false); notify('Tu número está verificado.');
    } catch (error) { setPhoneError((error as Error).message); } finally { setBusy(null); }
  }
  function changeNumber() { setSentTo(''); setCode(''); setPhoneError(''); setEditingPhone(true); requestAnimationFrame(() => phoneInput.current?.focus()); }
  return <main className={`page ${styles.profile}`}>
    <Link href="/" className={styles.back}><ArrowLeft size={16}/>Explorar tiendas</Link>
    <div className={styles.heading}><div><h1>Tu perfil</h1><p>Lo esencial para pedir y pasar a recoger.</p></div><Link href="/mis-pedidos">Mis pedidos<ArrowRight size={17}/></Link></div>
    {pageError && <p className={styles.error} role="alert">{pageError}</p>}
    <div className={styles.identity}><span className={styles.avatar} aria-hidden="true">{initials}</span><div><strong>{user.displayName}</strong><span>Tu cuenta en Foodies</span></div><span className={styles.identityStatus}><ShieldCheck size={17}/>{verified ? 'Celular verificado' : 'Verificación pendiente'}</span></div>
    <div className={styles.layout}>
      <section className={`${styles.details} t-acc`} aria-labelledby="profile-details-heading" data-open={detailsExpanded}>
        <div className={styles.detailsHeading}><h2 id="profile-details-heading">Datos personales</h2><button type="button" className={`${styles.detailsToggle} t-acc-head`} aria-label={detailsOpen ? 'Ocultar datos personales' : 'Editar datos personales'} aria-expanded={detailsExpanded} aria-controls="profile-details-body" onClick={() => setDetailsOpen(value => !value)}><ChevronDown className="t-acc-chevron" size={20}/></button></div>
        <div id="profile-details-body" className="t-acc-panel" inert={!detailsExpanded} aria-hidden={!detailsExpanded}><div className={`${styles.detailsBody} t-acc-panel-inner`}><div className={styles.detailsContent}><p>Tu nombre para cada pedido.</p>
        <form id="profile-details-form" onSubmit={event => { event.preventDefault(); void saveName(); }}>
          <label htmlFor="profile-name">Nombre</label><input id="profile-name" autoComplete="name" required maxLength={100} value={name} onChange={event => { setName(event.target.value); setSavedName(false); }}/>
          <label htmlFor="profile-email">Correo electrónico</label><div className={styles.emailField}><Mail size={17} aria-hidden="true"/><input id="profile-email" value={user.email} readOnly aria-describedby="profile-email-help"/></div><small id="profile-email-help">Vinculado a tu cuenta de Google.</small>
          {nameError && <p className={styles.error} role="alert">{nameError}</p>}
          <div className={styles.nameActions}><button className="button secondary" disabled={!!busy || !name.trim() || name.trim() === user.displayName}>{busy === 'name' ? 'Guardando…' : 'Guardar cambios'}</button>{savedName && <span className={styles.saved} role="status"><Check size={16}/>Guardado</span>}</div>
        </form></div></div></div>
      </section>
      <section className={styles.phonePanel} aria-labelledby="profile-phone-heading">
        <div className={styles.panelHeading}><h2 id="profile-phone-heading">{sentTo ? 'Revisa tus mensajes' : verified && !editing ? 'Todo listo para recoger' : 'Tu celular, a la mano'}</h2>{verified && !editing ? <CheckCheck size={23} aria-hidden="true"/> : <MessageSquare size={23} aria-hidden="true"/>}</div>
        <p>{sentTo ? 'Ingresa el PIN de 6 dígitos que solicitaste por SMS.' : auth?.smsConfigured ? 'Te avisamos por SMS cuando tu comida esté lista.' : 'Guarda un número de contacto para tus pedidos.'}</p>
        {verified && !editing ? <div className={styles.verifiedBlock}><span className={styles.verifiedLabel}><ShieldCheck size={17}/>Número verificado</span><strong>{user.phoneE164}</strong><button className={styles.textAction} onClick={changeNumber}><Pencil size={15}/>Cambiar número</button></div>
          : sentTo ? <div className={styles.pinStep}>
            <div className={styles.recipient}><span>{sentTo}</span><button className={styles.textAction} disabled={!!busy} onClick={changeNumber}>Cambiar</button></div>
            <form id="profile-pin-form" onSubmit={event => { event.preventDefault(); void confirmCode(); }}><label htmlFor="profile-pin">Código de verificación</label><input ref={pinInput} id="profile-pin" className={styles.pinInput} inputMode="numeric" autoComplete="one-time-code" required pattern="[0-9]{6}" maxLength={6} placeholder="000000" value={code} aria-describedby="profile-pin-help" aria-invalid={!!phoneError} onChange={event => setCode(event.target.value.replace(/\D/g,''))}/><small id="profile-pin-help"><Clock3 size={14}/>Válido por 5 minutos · Máximo 5 intentos</small>
              {phoneError && <p className={styles.error} role="alert">{phoneError}</p>}<button className="button full" disabled={!!busy || code.length !== 6}>{busy === 'confirm' ? 'Verificando…' : 'Verificar celular'}<Check size={17}/></button></form>
            <div className={styles.resend}><span>¿No llegó el mensaje?</span><button className={styles.textAction} disabled={!!busy || cooldown > 0} onClick={sendCode}>{cooldown > 0 ? `Reenviar en ${cooldown}s` : 'Reenviar código'}</button></div>
          </div> : <form id="profile-phone-form" className={styles.phoneForm} onSubmit={event => { event.preventDefault(); void sendCode(); }}>
            {user.phoneE164 && <p className={styles.previousNumber}>{verified ? 'Número actual' : 'Número sin verificar'}: {user.phoneE164}</p>}
            <label htmlFor="profile-phone">Número de celular</label><div className={styles.phoneField}><span id="phone-prefix" className={styles.prefix}>+52</span><input ref={phoneInput} id="profile-phone" type="tel" disabled={!!busy} inputMode="tel" autoComplete="tel-national" required pattern="[0-9]{10}" placeholder="33 1234 5678" value={phone} aria-describedby="phone-prefix profile-phone-help" aria-invalid={!!phoneError} onChange={event => { setPhone(localNumber(event.target.value)); setPhoneError(''); }}/></div><small id="profile-phone-help">México · Solo los 10 dígitos de tu número.</small>
            {phoneError && <p className={styles.error} role="alert">{phoneError}</p>}
            {auth?.phoneVerificationRequired && !auth.smsConfigured && <p className={styles.serviceNote}>El envío de códigos todavía no está disponible.</p>}
            {!auth?.phoneVerificationRequired && <p className={styles.serviceNote}>Por ahora se guardará sin verificar.</p>}
            <button className="button full" disabled={!!busy || (!!auth?.phoneVerificationRequired && (!auth.smsConfigured || cooldown > 0))}>{busy === 'send' ? 'Solicitando…' : !auth?.phoneVerificationRequired ? 'Guardar número' : cooldown > 0 ? `Solicitar en ${cooldown}s` : 'Enviar código por SMS'}<ArrowRight size={17}/></button>
            {verified && editingPhone && <button type="button" className={styles.textAction} disabled={!!busy} onClick={() => setEditingPhone(false)}>Conservar mi número actual</button>}
          </form>}
        <div className={styles.phoneFoot}><ShieldCheck size={16}/><span>{verified && !editing ? 'Puedes usar este número en tus próximos pedidos.' : 'Tu número se verifica al confirmar el PIN.'}</span></div>
      </section>
    </div>
    {returnTo !== '/' && <div className={styles.returnRow}><Link className={styles.returnLink} href={returnTo}>Volver a mi pedido<ArrowRight size={17}/></Link></div>}
  </main>;
}
