import { afterEach, beforeEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { JSDOM } from 'jsdom';
import React from 'react';

const dom = new JSDOM('<!doctype html><html><body></body></html>', { url: 'http://localhost', pretendToBeVisual: true });
const win = dom.window;
Object.assign(globalThis, {
  window: win, document: win.document, HTMLElement: win.HTMLElement,
  Event: win.Event, KeyboardEvent: win.KeyboardEvent, MutationObserver: win.MutationObserver,
  getComputedStyle: win.getComputedStyle.bind(win), requestAnimationFrame: win.requestAnimationFrame.bind(win),
  cancelAnimationFrame: win.cancelAnimationFrame.bind(win), IS_REACT_ACT_ENVIRONMENT: true,
});
Object.defineProperty(globalThis, 'navigator', { configurable: true, value: win.navigator });
let reduced = false;
win.matchMedia = () => ({ matches: reduced, addEventListener() {}, removeEventListener() {} });
Object.defineProperty(document, 'fonts', { value: { ready: Promise.resolve() } });
win.HTMLElement.prototype.getClientRects = function () { return this.isConnected ? [{ width: 80, height: 32 }] : []; };
for (const [property, field, fallback] of [['offsetLeft','left',0],['offsetTop','top',0],['offsetWidth','width',80],['offsetHeight','height',32]]) {
  Object.defineProperty(win.HTMLElement.prototype, property, { configurable: true, get() { return Number(this.dataset[field] ?? fallback); } });
}
const observers = new Set();
globalThis.ResizeObserver = class {
  constructor(callback) { this.callback = callback; observers.add(callback); }
  observe() {} disconnect() { observers.delete(this.callback); }
};
const { render, fireEvent, cleanup, waitFor, act } = await import('@testing-library/react');
const { MotionDialog, MotionToast, MotionTabs, MotionNumber, MotionToggle, MotionPanel } = await import('../src/components/motion.tsx');
const h = React.createElement;
// JSDOM has no layout engine; assertions target dialog semantics and state, not computed visibility.
const queryRole = (view, name) => view.baseElement.querySelector(name === 'textbox' ? 'input,textarea' : '[role="' + name + '"]');
function role(view, name) { const el = queryRole(view, name); assert.ok(el, 'Missing role: ' + name); return el; }
beforeEach(() => {
  reduced = false;
  document.documentElement.style.setProperty('--modal-close-dur', '150ms');
  document.documentElement.style.setProperty('--toast-close', '350ms');
  document.documentElement.style.setProperty('--panel-close-dur', '350ms');
});
afterEach(() => { cleanup(); observers.clear(); });

test('dialog preserves its form during exit, restores focus, and can open again', async () => {
  function Example() {
    const [open, setOpen] = React.useState(false);
    return h(React.Fragment, null, h('button', { onClick: () => setOpen(true) }, 'Abrir'),
      h(MotionDialog, { open, onClose: () => setOpen(false) }, h('section', { role: 'dialog', 'aria-label': 'Prueba' },
        h('input', { 'aria-label': 'Nombre', defaultValue: 'Ana' }), h('button', { onClick: () => setOpen(false) }, 'Cerrar'))));
  }
  const view = render(h(Example)); const trigger = view.getByText('Abrir'); trigger.focus(); fireEvent.click(trigger);
  await waitFor(() => assert.ok(role(view, 'dialog').classList.contains('is-open')));
  fireEvent.change(role(view, 'textbox'), { target: { value: 'Ana editada' } });
  fireEvent.keyDown(document, { key: 'Escape' });
  assert.ok(role(view, 'dialog').classList.contains('is-closing'));
  assert.ok(role(view, 'dialog').hasAttribute('inert'));
  assert.equal(role(view, 'textbox').value, 'Ana editada');
  await waitFor(() => assert.ok(queryRole(view, 'dialog') === null, 'Dialog is still mounted'));
  assert.ok(document.activeElement === trigger, 'Trigger focus was not restored'); assert.equal(document.body.style.overflow, '');
  fireEvent.click(trigger); await waitFor(() => assert.ok(role(view, 'dialog').classList.contains('is-open')));
  assert.equal(role(view, 'textbox').value, 'Ana');
});

test('keyboard focus stays inside dialog and clicks inside do not dismiss it', async () => {
  let dismissed = 0;
  const view = render(h(MotionDialog, { open: true, onClose: () => dismissed++ }, h('section', { role: 'dialog' }, h('button', null, 'Uno'), h('button', null, 'Dos'))));
  const first = await view.findByText('Uno'), last = view.getByText('Dos');
  last.focus(); fireEvent.keyDown(document, { key: 'Tab' }); assert.ok(document.activeElement === first, 'Focus did not wrap to first control');
  first.focus(); fireEvent.keyDown(document, { key: 'Tab', shiftKey: true }); assert.ok(document.activeElement === last, 'Focus did not wrap to last control');
  fireEvent.click(first); assert.equal(dismissed, 0);
  fireEvent.click(document.querySelector('.motion-overlay')); assert.equal(dismissed, 1);
});

test('rapid reopen cancels exit cleanup instead of removing the open dialog', async () => {
  const content = h('section', { role: 'dialog' }, 'Contenido');
  const view = render(h(MotionDialog, { open: true, onClose() {} }, content));
  await waitFor(() => assert.ok(role(view, 'dialog').classList.contains('is-open')));
  view.rerender(h(MotionDialog, { open: false, onClose() {} }, null));
  view.rerender(h(MotionDialog, { open: true, onClose() {} }, content));
  await act(async () => { await new Promise(resolve => setTimeout(resolve, 200)); });
  assert.ok(role(view, 'dialog').classList.contains('is-open'));
});

test('reduced motion opens immediately and removes exit delay', async () => {
  reduced = true; document.documentElement.style.setProperty('--modal-close-dur', '10s');
  const view = render(h(MotionDialog, { open: true, onClose() {} }, h('section', { role: 'dialog' }, 'Contenido')));
  assert.ok(role(view, 'dialog').classList.contains('is-open'));
  view.rerender(h(MotionDialog, { open: false, onClose() {} }, null));
  await waitFor(() => assert.ok(queryRole(view, 'dialog') === null, 'Dialog is still mounted'), { timeout: 250 });
});

test('repeated identical toast refreshes the live announcement and retains text during exit', async () => {
  const view = render(h(MotionToast, { message: 'Agregado', version: 1, onDismiss() {} }));
  const old = await waitFor(() => role(view, 'status'));
  view.rerender(h(MotionToast, { message: 'Agregado', version: 2, onDismiss() {} }));
  assert.ok(role(view, 'status') !== old, 'Toast announcement did not refresh'); assert.equal(role(view, 'status').textContent, 'Agregado');
  view.rerender(h(MotionToast, { message: '', version: 2, onDismiss() {} }));
  assert.equal(role(view, 'status').textContent, 'Agregado');
  await waitFor(() => assert.ok(queryRole(view, 'status') === null, 'Toast is still mounted'));
});

test('sliding indicator follows selection and recalculates after layout changes', async () => {
  function bar(value) { return h(MotionTabs, { activeKey: value, role: 'group' },
    h('button', { className: value === 'a' ? 'selected' : '', 'data-left': 0 }, 'A'),
    h('button', { className: value === 'b' ? 'selected' : '', 'data-left': 100, 'data-width': 110 }, 'B')); }
  const view = render(bar('a')); await act(async () => { await document.fonts.ready; });
  const pill = view.container.querySelector('.t-tabs-pill'); assert.equal(pill.style.transform, 'translateX(0px)');
  view.rerender(bar('b')); assert.equal(pill.style.transform, 'translateX(100px)'); assert.equal(pill.style.width, '110px');
  const selected = view.getByText('B'); assert.equal(selected.getAttribute('aria-pressed'), 'true'); selected.dataset.left = '120';
  act(() => { for (const callback of observers) callback(); }); assert.equal(pill.style.transform, 'translateX(120px)');
});

test('only changed digits animate and polling the same value does not rebuild the counter', () => {
  const view = render(h(MotionNumber, { value: 129 }));
  const group = view.container.querySelector('.t-digit-group'); assert.ok(!group.classList.contains('is-animating'));
  view.rerender(h(MotionNumber, { value: 130 }));
  const digits = group.querySelectorAll('.t-digit'); assert.ok(digits[0].hasAttribute('data-same')); assert.ok(!digits[1].hasAttribute('data-same'));
  assert.ok(group.classList.contains('is-animating'));
  view.rerender(h(MotionNumber, { value: 130 })); assert.ok(view.container.querySelector('.t-digit-group') === group, 'Counter was rebuilt');
  assert.equal(view.container.querySelector('.sr-only').textContent, '130');
});

test('availability switch has no initial bounce and reflects the confirmed state', () => {
  const view = render(h(MotionToggle, { on: false, label: 'Disponible', onChange() {} }));
  const control = role(view, 'switch'); assert.ok(!control.classList.contains('is-init')); assert.equal(control.getAttribute('aria-checked'), 'false');
  view.rerender(h(MotionToggle, { on: true, label: 'Disponible', onChange() {} }));
  assert.ok(control.classList.contains('is-init')); assert.equal(control.getAttribute('aria-checked'), 'true');
});

test('checkout panel becomes inert immediately during exit', async () => {
  const view = render(h(MotionPanel, { open: true }, h('button', null, 'Confirmar')));
  await view.findByText('Confirmar'); view.rerender(h(MotionPanel, { open: false }, null));
  assert.ok(view.container.querySelector('.t-panel-slide').hasAttribute('inert'));
  await waitFor(() => assert.ok(view.queryByText('Confirmar') === null, 'Panel is still mounted'));
});
