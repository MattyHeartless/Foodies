import { test } from 'node:test';
import assert from 'node:assert/strict';
import { webcrypto } from 'node:crypto';
import { createCartKey } from '../src/lib/cart-key.ts';

test('uses the native UUID generator when available', (t) => {
  const expected = 'cf327055-5549-43ad-a9de-0cc7d897924d';
  t.mock.getter(globalThis, 'crypto', () => ({ randomUUID: () => expected }));
  assert.equal(createCartKey(), expected);
});

test('cart updates get distinct valid keys when randomUUID is unavailable over HTTP', (t) => {
  t.mock.getter(globalThis, 'crypto', () => ({
    getRandomValues: bytes => webcrypto.getRandomValues(bytes),
  }));
  const keys = Array.from({ length: 1000 }, () => createCartKey());
  for (const key of keys) {
    assert.match(key, /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  }
  assert.equal(new Set(keys).size, keys.length);
});
