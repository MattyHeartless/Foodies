'use client';

import { Children, cloneElement, isValidElement, useLayoutEffect, useRef, useState, type HTMLAttributes, type ReactElement, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Check, X } from 'lucide-react';

function reducedMotion() { return window.matchMedia('(prefers-reduced-motion: reduce)').matches; }
function duration(token: string) {
  if (reducedMotion()) return 0;
  const value = getComputedStyle(document.documentElement).getPropertyValue(token).trim();
  const amount = parseFloat(value);
  return Number.isFinite(amount) ? amount * (value.endsWith('ms') ? 1 : 1000) : 0;
}

// Keep the surface mounted through its exit, reading the same clock CSS uses.
function usePresence(open: boolean, closeToken: string) {
  const [present, setPresent] = useState(false);
  const [phase, setPhase] = useState('');
  useLayoutEffect(() => {
    if (open) {
      setPresent(true); setPhase('');
      if (reducedMotion()) { setPhase('is-open'); return; }
      let next = 0;
      const frame = requestAnimationFrame(() => { next = requestAnimationFrame(() => setPhase('is-open')); });
      return () => { cancelAnimationFrame(frame); cancelAnimationFrame(next); };
    }
    setPhase('is-closing');
    const timeout = setTimeout(() => { setPresent(false); setPhase(''); }, duration(closeToken));
    return () => clearTimeout(timeout);
  }, [open, closeToken]);
  return { present, phase };
}

export function MotionDialog({ open, onClose, children }: { open: boolean; onClose: () => void; children: ReactNode }) {
  const { present, phase } = usePresence(open, '--modal-close-dur');
  const overlay = useRef<HTMLDivElement>(null);
  const content = useRef<ReactNode>(null);
  const close = useRef(onClose); close.current = onClose;
  const returnFocus = useRef<HTMLElement | null>(null);
  if (open) content.current = children;
  useLayoutEffect(() => {
    if (open && !present) returnFocus.current = document.activeElement as HTMLElement;
  }, [open, present]);
  useLayoutEffect(() => {
    if (!present) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const root = overlay.current;
    const focusable = () => Array.from(root?.querySelectorAll<HTMLElement>('a[href], button:not(:disabled), input:not(:disabled), textarea:not(:disabled), select:not(:disabled), [tabindex="0"]') || []).filter(el => el.getClientRects().length > 0 && !el.closest('[inert]'));
    const initial = root?.querySelector<HTMLElement>('[autofocus]') || focusable()[0];
    initial?.focus({ preventScroll: true });
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); close.current(); }
      if (event.key !== 'Tab') return;
      const items = focusable(); const first = items[0]; const last = items.at(-1);
      if (!first) { event.preventDefault(); return; }
      if (event.shiftKey && (document.activeElement === first || !root?.contains(document.activeElement))) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && (document.activeElement === last || !root?.contains(document.activeElement))) { event.preventDefault(); first.focus(); }
    };
    document.addEventListener('keydown', onKey);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener('keydown', onKey);
      if (returnFocus.current?.isConnected) returnFocus.current.focus({ preventScroll: true });
    };
  }, [present]);
  if (!present || !isValidElement(content.current)) return null;
  const child = content.current as ReactElement<{ className?: string; inert?: boolean }>;
  return createPortal(<div ref={overlay} className={`overlay motion-overlay ${phase}`} onClick={e => { if (e.target === e.currentTarget && open) close.current(); }}>
    <div className={`modal-shell t-modal ${phase} ${child.props.className?.includes('wide-modal') ? 'modal-shell-wide' : ''}`}>
      {cloneElement(child, { className: `${child.props.className || ''} ${phase}`, inert: !open })}
    </div>
  </div>, document.body);
}

export function MotionToast({ message, version, onDismiss }: { message: string; version: number; onDismiss: () => void }) {
  const { present, phase } = usePresence(!!message, '--toast-close');
  const previous = useRef(message); if (message) previous.current = message;
  if (!present) return null;
  return <div className="toast-position"><div className={`toast t-toast ${phase}`}>
    <Check size={18} className="toast-check" aria-hidden="true"/>
    <span key={version} className="toast-message" role="status" aria-live="polite" aria-atomic="true">{previous.current}</span>
    <button className="icon" aria-label="Cerrar aviso" onClick={onDismiss}><X size={15}/></button>
  </div></div>;
}

// Unchanged leading digits stay still; network refreshes do not replay counters.
export function MotionNumber({ value }: { value: string | number }) {
  const text = String(value); const group = useRef<HTMLSpanElement>(null); const previous = useRef(text);
  useLayoutEffect(() => {
    const el = group.current; if (!el) return;
    const old = previous.current; let same = 0;
    while (same < old.length && same < text.length && old[same] === text[same]) same++;
    el.classList.remove('is-animating');
    const digits = el.querySelectorAll<HTMLElement>('.t-digit');
    digits.forEach((digit, i) => { if (i < same) digit.dataset.same = ''; else delete digit.dataset.same; });
    if (old !== text) { void el.offsetHeight; el.classList.add('is-animating'); }
    previous.current = text;
  }, [text]);
  return <span className="motion-number"><span className="sr-only">{text}</span><span className="t-digit-group" ref={group} aria-hidden="true">{text.split('').map((char, i) => <span className="t-digit" key={i} data-stagger={i === text.length - 2 ? '1' : i === text.length - 1 ? '2' : undefined}>{char}</span>)}</span></span>;
}

export function MotionTabs({ activeKey, as: Tag = 'div', children, className = '', ...props }: HTMLAttributes<HTMLElement> & { activeKey: string; as?: 'div' | 'nav'; children: ReactNode }) {
  const bar = useRef<HTMLDivElement>(null); const pill = useRef<HTMLSpanElement>(null);
  const position = useRef<(animate: boolean) => void>(() => {});
  const previousKey = useRef(activeKey);
  useLayoutEffect(() => {
    const root = bar.current; const indicator = pill.current; if (!root || !indicator) return;
    const move = (animate: boolean) => {
      const active = root.querySelector<HTMLElement>('.selected');
      if (!active) { indicator.style.opacity = '0'; return; }
      const oldTransition = indicator.style.transition;
      if (!animate) indicator.style.transition = 'none';
      indicator.style.transform = `translateX(${active.offsetLeft}px)`;
      indicator.style.width = `${active.offsetWidth}px`; indicator.style.height = `${active.offsetHeight}px`; indicator.style.top = `${active.offsetTop}px`; indicator.style.opacity = '1';
      if (!animate) { void indicator.offsetWidth; indicator.style.transition = oldTransition; }
      root.dataset.ready = 'true';
    };
    position.current = move; move(false);
    const observer = new ResizeObserver(() => move(false)); observer.observe(root);
    root.querySelectorAll('button, a').forEach(el => observer.observe(el));
    let active = true; void document.fonts.ready.then(() => { if (active) move(false); });
    return () => { active = false; observer.disconnect(); };
  }, []);
  useLayoutEffect(() => {
    if (previousKey.current !== activeKey) position.current(true);
    previousKey.current = activeKey;
  }, [activeKey]);
  const controls = Children.map(children, child => {
    if (!isValidElement(child)) return child;
    const control = child as ReactElement<{ className?: string; 'aria-pressed'?: boolean; 'aria-current'?: 'page' }>;
    const selected = control.props.className?.split(' ').includes('selected') || false;
    if (child.type === 'button') return cloneElement(control, { 'aria-pressed': selected });
    if (Tag === 'nav' && selected) return cloneElement(control, { 'aria-current': 'page' });
    return child;
  });
  return <Tag ref={bar} {...props} className={`${className} t-tabs`}><span ref={pill} className="t-tabs-pill" aria-hidden="true"/>{controls}</Tag>;
}

export function MotionToggle({ on, disabled, label, onChange }: { on: boolean; disabled?: boolean; label: string; onChange: () => void }) {
  const button = useRef<HTMLButtonElement>(null); const previous = useRef(on);
  useLayoutEffect(() => {
    if (previous.current !== on) button.current?.classList.add('is-init');
    previous.current = on;
  }, [on]);
  return <button ref={button} type="button" role="switch" aria-checked={on} aria-label={label} className={`switch t-toggle ${on ? 'on' : ''}`} data-on={on} disabled={disabled} onClick={onChange}><span className="t-toggle-thumb"/></button>;
}

export function MotionPanel({ open, children, className = '' }: { open: boolean; children: ReactNode; className?: string }) {
  const { present, phase } = usePresence(open, '--panel-close-dur');
  const content = useRef(children); if (open) content.current = children;
  if (!present) return null;
  return <div className="motion-panel-clip"><div className={`${className} t-panel-slide`} data-open={phase === 'is-open'} inert={!open}>{content.current}</div></div>;
}
