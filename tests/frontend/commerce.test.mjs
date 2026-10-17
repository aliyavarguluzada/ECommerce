import test from 'node:test';
import assert from 'node:assert/strict';
import {escapeHtml, money, problemMessage, canCancel, pageQuery, artwork} from '../../src/ECommerce.ApiGateway/wwwroot/shop/commerce.mjs';

test('product and account text cannot inject HTML or attribute values', () => {
  assert.equal(escapeHtml('<img src=x onerror="alert(1)">'), '&lt;img src=x onerror=&quot;alert(1)&quot;&gt;');
  assert.equal(escapeHtml("O'Brien & Co"), 'O&#39;Brien &amp; Co');
  assert.equal(escapeHtml(null), '');
});

test('currency display preserves decimal values', () => {
  assert.equal(money(10.48, 'USD'), '$10.48');
  assert.equal(money(0, 'USD'), '$0.00');
});

test('validation errors explain rejected requests', () => {
  assert.equal(problemMessage({errors: {Name: ['Name is required.'], Price: ['Price is invalid.']}}), 'Name is required. Price is invalid.');
  assert.equal(problemMessage({detail: 'Insufficient stock.', title: 'Conflict'}), 'Insufficient stock.');
});

test('only cancellable order states expose cancellation', () => {
  assert.equal(canCancel('Reserved'), true);
  assert.equal(canCancel('Confirmed'), true);
  for (const status of ['Shipped', 'Delivered', 'Cancelled', 'Confirming', 'Cancelling']) {
    assert.equal(canCancel(status), false);
  }
});

test('filters are encoded and zero/false values are not lost', () => {
  const query = pageQuery(3, 20, {search: 'a&b', isActive: false, maxAvailable: 0, categoryId: ''});
  assert.equal(query.get('search'), 'a&b');
  assert.equal(query.get('isActive'), 'false');
  assert.equal(query.get('maxAvailable'), '0');
  assert.equal(query.has('categoryId'), false);
});

test('illustrations never embed product names as HTML', () => {
  const svg = artwork('<script>alert(1)</script>');
  assert.match(svg, /^<svg /);
  assert.equal(svg.includes('<script>'), false);
});
