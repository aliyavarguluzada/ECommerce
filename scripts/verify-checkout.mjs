import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {createApi, adminSession, saveReport} from './lib/http.mjs';

if (process.env.ECOMMERCE_WRITE_SMOKE !== '1') {
  throw new Error('This creates labelled test records. Set ECOMMERCE_WRITE_SMOKE=1 to run it against your development seed stack.');
}

const api = createApi();
const admin = await adminSession(api);
const adminToken = admin.accessToken;
const runId = randomUUID();
const password = `Smoke-${randomUUID()}!`;
const report = {runId, checks: [], records: {}};
const sessions = [admin];
const products = [];
const pendingOrders = new Set();
let customer;
let category;

async function record(check, fn) {
  await fn();
  report.checks.push({check, result: 'passed'});
  console.log(`PASS ${check}`);
}

async function product(name, quantity) {
  const {data} = await api('/api/products', {method: 'POST', token: adminToken, body: {name: `Smoke ${name} ${runId}`, description: 'Labelled checkout verification fixture.', categoryId: category.id, price: 12.5, currency: 'USD'}});
  products.push(data.id);
  await api('/api/inventory/stock', {method: 'POST', token: adminToken, body: {productId: data.id, quantity}});
  return data;
}

const address = {recipient: 'Smoke Customer', line1: '1 Test Street', line2: null, city: 'Baku', region: null, postalCode: 'AZ1000', countryCode: 'AZ'};

try {
  await record('anonymous writes are rejected', async () => {
    const response = await api('/api/cart', {allow: [401]});
    assert.equal(response.status, 401);
  });

  const registered = await api('/api/identity/register', {method: 'POST', body: {email: `smoke-${runId}@example.test`, password, displayName: 'Smoke Customer'}});
  customer = registered.data;
  sessions.push(customer);
  report.records.customerId = customer.user.id;
  category = (await api('/api/categories', {method: 'POST', token: adminToken, body: {name: `Smoke ${runId}`, description: 'Isolated verification fixtures.'}})).data;

  const item = await product('checkout', 10);
  let order;
  let payment;
  await record('cart checkout reserves stock and preserves totals', async () => {
    await api(`/api/cart/items/${item.id}`, {method: 'PUT', token: customer.accessToken, body: {quantity: 2}});
    const {data: cart} = await api('/api/cart', {token: customer.accessToken});
    assert.equal(cart.subtotal, 25);
    const command = {requestId: randomUUID(), shippingAddress: address};
    order = (await api('/api/cart/checkout', {method: 'POST', token: customer.accessToken, body: command})).data;
    pendingOrders.add(order.id);
    assert.equal(order.status, 'Reserved');
    assert.equal(order.total, 25);
    const replay = (await api('/api/cart/checkout', {method: 'POST', token: customer.accessToken, body: command})).data;
    assert.equal(replay.id, order.id);
    const {data: stock} = await api(`/api/inventory/stock/${item.id}`, {token: adminToken});
    assert.equal(stock.reserved, 2);
    assert.equal(stock.onHand, 10);
  });

  await record('payment confirms once and commits exactly two units', async () => {
    const command = {requestId: randomUUID(), orderId: order.id, methodToken: 'dev-approved'};
    payment = (await api('/api/payments', {method: 'POST', token: customer.accessToken, body: command})).data;
    assert.equal(payment.status, 'Succeeded');
    assert.equal(payment.orderNotified, true);
    const replay = (await api('/api/payments', {method: 'POST', token: customer.accessToken, body: command})).data;
    assert.equal(replay.id, payment.id);
    const {data: paidOrder} = await api(`/api/orders/${order.id}`, {token: customer.accessToken});
    assert.equal(paidOrder.status, 'Confirmed');
    const {data: stock} = await api(`/api/inventory/stock/${item.id}`, {token: adminToken});
    assert.equal(stock.onHand, 8);
    assert.equal(stock.reserved, 0);
    report.records.deliveredOrderId = order.id;
    report.records.paymentId = payment.id;
  });

  await record('customer cannot fulfill orders; admin can ship and deliver', async () => {
    const forbidden = await api(`/api/orders/${order.id}/ship`, {method: 'POST', token: customer.accessToken, body: {carrier: 'Smoke Carrier', trackingNumber: runId}, allow: [403]});
    assert.equal(forbidden.status, 403);
    const shipped = (await api(`/api/orders/${order.id}/ship`, {method: 'POST', token: adminToken, body: {carrier: 'Smoke Carrier', trackingNumber: runId}})).data;
    assert.equal(shipped.status, 'Shipped');
    const delivered = (await api(`/api/orders/${order.id}/deliver`, {method: 'POST', token: adminToken})).data;
    assert.equal(delivered.status, 'Delivered');
    pendingOrders.delete(order.id);
  });

  await record('declined payment and cancellation release reserved stock', async () => {
    const requestId = randomUUID();
    const {data: declinedOrder} = await api('/api/orders', {method: 'POST', token: customer.accessToken, body: {requestId, shippingAddress: address, items: [{productId: item.id, quantity: 1}]}});
    pendingOrders.add(declinedOrder.id);
    const declined = await api('/api/payments', {method: 'POST', token: customer.accessToken, body: {requestId: randomUUID(), orderId: declinedOrder.id, methodToken: 'dev-declined'}, allow: [402]});
    assert.equal(declined.status, 402);
    const cancelled = (await api(`/api/orders/${declinedOrder.id}/cancel`, {method: 'POST', token: customer.accessToken, body: {reason: 'Smoke verification completed.'}})).data;
    assert.equal(cancelled.status, 'Cancelled');
    pendingOrders.delete(declinedOrder.id);
    const {data: stock} = await api(`/api/inventory/stock/${item.id}`, {token: adminToken});
    assert.equal(stock.onHand, 8);
    assert.equal(stock.reserved, 0);
  });

  await record('eight competing orders cannot oversell five units', async () => {
    const limited = await product('concurrency', 5);
    const results = await Promise.all(Array.from({length: 8}, async () => {
      const requestId = randomUUID();
      pendingOrders.add(requestId);
      const {data} = await api('/api/orders', {method: 'POST', token: customer.accessToken, body: {requestId, shippingAddress: address, items: [{productId: limited.id, quantity: 4}]}});
      return data;
    }));
    assert.equal(results.filter(order => order.status === 'Reserved').length, 1);
    assert.equal(results.filter(order => order.status === 'PendingInventory').length, 7);
    const {data: stock} = await api(`/api/inventory/stock/${limited.id}`, {token: adminToken});
    assert.equal(stock.onHand, 5);
    assert.equal(stock.reserved, 4);
    assert.equal(stock.available, 1);
    for (const result of results) {
      if (result.status !== 'Cancelled') {
        await api(`/api/orders/${result.id}/cancel`, {method: 'POST', token: customer.accessToken, body: {reason: 'Release concurrency fixture.'}});
      }
      pendingOrders.delete(result.id);
    }
    const {data: released} = await api(`/api/inventory/stock/${limited.id}`, {token: adminToken});
    assert.equal(released.available, 5);
    assert.equal(released.reserved, 0);
  });

  report.result = 'passed';
} catch (error) {
  report.result = 'failed';
  report.error = error.message;
  process.exitCode = 1;
  console.error(error.message);
} finally {
  const cleanupErrors = [];
  for (const id of pendingOrders) {
    try {
      await api(`/api/orders/${id}/cancel`, {method: 'POST', token: adminToken, body: {reason: 'Smoke cleanup.'}, allow: [404, 409]});
    } catch (error) {
      cleanupErrors.push(error.message);
    }
  }
  for (const id of products) {
    try {
      await api(`/api/products/${id}`, {method: 'DELETE', token: adminToken});
    } catch (error) {
      cleanupErrors.push(error.message);
    }
  }
  for (const session of sessions) {
    try {
      await api('/api/identity/logout', {method: 'POST', token: session.accessToken, body: {refreshToken: session.refreshToken}});
    } catch (error) {
      cleanupErrors.push(error.message);
    }
  }
  report.cleanup = {productFixtures: 'discontinued; historical records retained', errors: cleanupErrors};
  if (cleanupErrors.length) {
    process.exitCode = 1;
  }
  await saveReport('checkout-report.json', report);
}
