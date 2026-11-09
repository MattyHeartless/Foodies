import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { JSDOM } from 'jsdom';
import React from 'react';
const dom = new JSDOM('<!doctype html><html><body></body></html>', { url: 'http://localhost' });
Object.assign(globalThis, { window: dom.window, document: dom.window.document, HTMLElement: dom.window.HTMLElement, IS_REACT_ACT_ENVIRONMENT: true });
Object.defineProperty(globalThis, 'navigator', { configurable: true, value: dom.window.navigator });
const { render, cleanup, fireEvent } = await import('@testing-library/react');
const { StoreHours } = await import('../src/components/store-hours.tsx');
const { storeWeekday, dayIntervals } = await import('../src/lib/store-hours.ts');
afterEach(() => cleanup());
const h = React.createElement;
const referenceTime = new Date('2026-11-05T02:00:00Z'); // Wednesday evening at the store; Thursday in UTC.
const base = { operationalState: 'Abierta', timeZoneId: 'America/Mexico_City', hours: Array.from({ length: 7 }, (_, dayOfWeek) => ({ dayOfWeek, openingTime: '08:00:00', closingTime: '20:00:00', isClosed: false })) };

test('today follows the store timezone rather than the device date', () => {
  assert.equal(storeWeekday(base.timeZoneId, referenceTime), 3);
  const { container } = render(h(StoreHours, { store: base, referenceTime }));
  assert.equal(container.querySelector('.store-hours-day').textContent, 'Lun–Dom');
  assert.equal(container.querySelector('.store-hours-row.is-today dt').textContent, 'MiércolesHoy');
  assert.equal(container.querySelector('.store-hours-time').textContent, '08:00–20:00');
});

test('week accordion stays expanded on refresh and hides its content on close', () => {
  const { container, rerender } = render(h(StoreHours, { store: base, referenceTime }));
  const trigger = container.querySelector('button');
  const panel = container.querySelector('.t-acc-panel');
  assert.equal(trigger.getAttribute('aria-expanded'), 'false');
  assert.ok(panel.hasAttribute('inert'));
  assert.equal(trigger.getAttribute('aria-controls'), panel.id);
  fireEvent.click(trigger);
  assert.equal(trigger.getAttribute('aria-expanded'), 'true');
  assert.ok(!panel.hasAttribute('inert'));
  assert.equal(panel.querySelectorAll('dt').length, 7);
  assert.equal(panel.querySelector('dt').textContent, 'Lunes');
  rerender(h(StoreHours, { store: { ...base, operationalState: 'Pausada' }, referenceTime }));
  assert.equal(trigger.getAttribute('aria-expanded'), 'true', 'The week must stay expanded when the store refreshes');
  fireEvent.click(trigger);
  assert.equal(trigger.getAttribute('aria-expanded'), 'false');
  assert.equal(panel.getAttribute('aria-hidden'), 'true');
  assert.ok(panel.hasAttribute('inert'));
});

test('split schedules are sorted and all intervals remain visible without changing input', () => {
  const store = { ...base, hours: [{ dayOfWeek: 3, openingTime: '14:00:00', closingTime: '20:00:00', isClosed: false }, { dayOfWeek: 3, openingTime: '08:00:00', closingTime: '12:00:00', isClosed: false }] };
  const { container } = render(h(StoreHours, { store, referenceTime }));
  assert.deepEqual([...container.querySelectorAll('.store-hours-time > span')].map(p => p.textContent), ['08:00–12:00', '14:00–20:00']);
  assert.equal(store.hours[0].openingTime, '14:00:00');
  assert.equal(container.querySelector('.store-hours-day').textContent, 'Hoy');
});

test('closed days and overnight closing times are explicit', () => {
  assert.deepEqual(dayIntervals([{ dayOfWeek: 3, openingTime: '22:00:00', closingTime: '02:00:00', isClosed: false }], 3), ['22:00 – 02:00 (+1 día)']);
  const store = { ...base, hours: base.hours.map(row => ({ ...row, isClosed: row.dayOfWeek === 3 })) };
  const { container } = render(h(StoreHours, { store, referenceTime }));
  assert.equal(container.querySelector('.store-hours-time').textContent, 'Cerrado hoy');
  assert.equal(container.querySelector('.is-today dd').textContent, 'Cerrado');
});

test('paused state comes from API even while the published schedule is open', () => {
  const { container } = render(h(StoreHours, { store: { ...base, operationalState: 'Pausada' }, referenceTime }));
  assert.equal(container.querySelector('.store-hours-state').textContent, 'En pausa');
  assert.equal(container.querySelector('.store-hours-time').textContent, '08:00–20:00');
});

test('missing hours are not replaced with invented opening times', () => {
  const { container } = render(h(StoreHours, { store: { ...base, hours: [] }, referenceTime }));
  assert.equal(container.querySelector('.store-hours-time').textContent, 'Por confirmar');
  assert.ok(container.querySelector('button') === null);
  assert.ok(container.querySelector('.store-hours-chevron') === null);
});
