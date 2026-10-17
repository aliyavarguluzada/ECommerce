import {escapeHtml as e, money, problemMessage, canCancel, pageQuery, artwork} from './commerce.mjs';

const main = document.querySelector('#main');
const modal = document.querySelector('#modal');
const state = {auth: null, cart: null, page: 1, search: '', category: '', adminTab: 'products', categories: [], products: [], orders: [], inventory: [], checkout: null};
let renderVersion = 0;
let refreshPromise;
let noticeTimer;
const paymentIds = new Map();

const isAdmin = () => state.auth?.user.roles.includes('Admin');
const route = () => ['orders', 'admin'].includes(location.hash.slice(1)) ? location.hash.slice(1) : 'shop';
const button = (label, action, extra = '', classes = 'secondary small') => `<button type="button" class="button ${classes}" data-action="${action}" ${extra}>${label}</button>`;
const badge = status => `<span class="badge ${e(status)}">${e(status)}</span>`;
const field = (label, name, value = '', attributes = '') => `<label>${label}<input name="${name}" value="${e(value)}" ${attributes}></label>`;
const errorBlock = '<p class="form-error" role="alert" hidden></p>';
const date = value => new Intl.DateTimeFormat('en', {dateStyle: 'medium'}).format(new Date(value));

async function request(path, {method = 'GET', body, anonymous = false, retry = true} = {}) {
  const headers = {'Accept': 'application/json'};
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }
  if (!anonymous && state.auth) {
    headers.Authorization = `Bearer ${state.auth.accessToken}`;
  }
  const response = await fetch(path, {method, headers, body: body === undefined ? undefined : JSON.stringify(body)});
  if (response.status === 401 && !anonymous && state.auth && retry) {
    refreshPromise ??= request('/api/identity/refresh', {method: 'POST', body: {refreshToken: state.auth.refreshToken}, anonymous: true}).finally(() => { refreshPromise = null; });
    try {
      state.auth = await refreshPromise;
    } catch (error) {
      state.auth = null;
      state.cart = null;
      updateHeader();
      throw error;
    }
    return await request(path, {method, body, retry: false});
  }
  const text = await response.text();
  let result;
  try {
    result = text ? JSON.parse(text) : null;
  } catch {
    throw new Error('The service returned an unexpected response. Please try again.');
  }
  if (!response.ok) {
    const error = new Error(problemMessage(result, `Request failed (${response.status}).`));
    error.status = response.status;
    throw error;
  }
  return result;
}

function notify(message, error = false) {
  const notice = document.querySelector('#notice');
  notice.textContent = message;
  notice.className = error ? 'error' : '';
  notice.hidden = false;
  clearTimeout(noticeTimer);
  noticeTimer = setTimeout(() => { notice.hidden = true; }, 6000);
}

function updateHeader() {
  document.querySelector('#account-button').textContent = state.auth ? `${state.auth.user.displayName} · Sign out` : 'Sign in';
  document.querySelector('#admin-link').hidden = !isAdmin();
  document.querySelector('#cart-count').textContent = state.cart?.items.reduce((sum, item) => sum + item.quantity, 0) ?? 0;
  for (const link of document.querySelectorAll('[data-nav]')) {
    link.classList.toggle('active', link.dataset.nav === route());
  }
}

function openModal(title, html, drawer = false) {
  modal.className = drawer ? 'drawer' : '';
  modal.innerHTML = `<div class="modal-header"><h2 id="modal-title">${e(title)}</h2><button type="button" class="close-button" data-action="close" aria-label="Close dialog">×</button></div>${html}`;
  if (!modal.open) {
    modal.showModal();
  }
}

function authDialog(register = false) {
  openModal(register ? 'Make yourself at home.' : 'Welcome back.', `<p class="form-note">${register ? 'Create an account to save your bag and track your orders.' : 'Sign in to pick up where you left off.'}</p><form class="form" data-form="${register ? 'register' : 'login'}">
    ${register ? field('Your name', 'displayName', '', 'required maxlength="150" autocomplete="name"') : ''}
    ${field('Email address', 'email', '', 'type="email" required maxlength="256" autocomplete="username"')}
    ${field('Password', 'password', '', `type="password" required autocomplete="${register ? 'new-password' : 'current-password'}" ${register ? 'minlength="12"' : ''}`)}
    ${register ? '<p class="quiet">Use at least 12 characters, with uppercase, lowercase, a number, and a symbol.</p>' : ''}
    ${errorBlock}<button class="button accent">${register ? 'Create account' : 'Sign in'}</button></form>
    <div class="divider"></div>${button(register ? 'Already a member? Sign in' : 'New here? Create an account', register ? 'login-dialog' : 'register-dialog', '', 'secondary full')}`);
}

async function refreshCart() {
  state.cart = state.auth ? await request('/api/cart') : null;
  updateHeader();
}

async function cartDialog() {
  if (!state.auth) {
    authDialog();
    return;
  }
  await refreshCart();
  const cart = state.cart;
  const lines = cart.items.map(item => `<div class="cart-line"><div class="cart-thumb">${artwork(item.productName)}</div><div><h3>${e(item.productName || 'Unavailable product')}</h3><p>${item.isAvailable ? money(item.unitPrice, item.currency) : 'Currently unavailable'}</p><div class="quantity-controls">${button('−', 'quantity', `data-id="${e(item.productId)}" data-quantity="${item.quantity - 1}"`, 'secondary small')}<span>${item.quantity}</span>${button('+', 'quantity', `data-id="${e(item.productId)}" data-quantity="${item.quantity + 1}"`, 'secondary small')}</div></div>${button('Remove', 'quantity', `data-id="${e(item.productId)}" data-quantity="0"`, 'secondary small')}</div>`).join('');
  openModal('Your bag', cart.items.length ? `${lines}<div class="cart-summary"><span>Subtotal</span><strong>${cart.subtotal === null ? 'Unavailable' : money(cart.subtotal, cart.currency)}</strong></div><p class="form-note">Development checkout uses simulated payments. No real money is charged.</p>${cart.pendingCheckoutId ? button('Recover pending checkout', 'retry-checkout', '', 'accent full') : button('Continue to checkout →', 'checkout-dialog', cart.canCheckout ? '' : 'disabled', 'accent full')}` : '<div class="empty"><h3>A little room for something good.</h3><p>Your bag is empty.</p></div>', true);
}

function checkoutDialog() {
  if (!state.auth) {
    authDialog();
    return;
  }
  openModal('The final details.', `<p class="form-note">Your bag total: <strong>${money(state.cart?.subtotal, state.cart?.currency || 'USD')}</strong>. Your prices will be checked again at checkout.</p><form class="form" data-form="checkout">
    ${field('Recipient', 'recipient', state.auth.user.displayName, 'required maxlength="150" autocomplete="shipping name"')}
    ${field('Street address', 'line1', '', 'required maxlength="200" autocomplete="shipping address-line1"')}
    ${field('Apartment, suite (optional)', 'line2', '', 'maxlength="200" autocomplete="shipping address-line2"')}
    <div class="form-row">${field('City', 'city', '', 'required maxlength="100" autocomplete="shipping address-level2"')}${field('Region (optional)', 'region', '', 'maxlength="100" autocomplete="shipping address-level1"')}</div>
    <div class="form-row">${field('Postal code', 'postalCode', '', 'required maxlength="20" autocomplete="shipping postal-code"')}${field('Country code', 'countryCode', 'AZ', 'required minlength="2" maxlength="2" autocomplete="shipping country"')}</div>
    <p class="form-note">Submitting reserves stock and creates your order. Choose a simulated payment on the next screen.</p>${errorBlock}<button class="button accent">Create order →</button></form>`);
}

function paymentDialog(order) {
  openModal('One last step.', `<div class="success-symbol">✓</div><p>Order <strong>${e(order.id.slice(0, 8))}</strong> is ${e(order.status.toLowerCase())}.</p><div class="cart-summary"><span>Order total</span><strong>${money(order.total, order.currency)}</strong></div>${order.status === 'Reserved' ? `<form class="form" data-form="payment" data-id="${e(order.id)}"><label>Simulated payment<select name="methodToken"><option value="dev-approved">Approve payment</option><option value="dev-declined">Decline payment (test)</option></select></label><p class="form-note">Development provider only. No card details or real charges.</p>${errorBlock}<button class="button accent">Pay ${money(order.total, order.currency)}</button></form>` : '<p class="form-note">Stock reservation is still processing. Open your orders to check its status or retry.</p>'}<div class="divider"></div>${button('View my orders', 'go-orders', '', 'secondary full')}`);
}

function pageControls(total, size = 12) {
  const pages = Math.max(1, Math.ceil(total / size));
  return `<div class="pagination">${button('← Previous', 'page', `data-page="${state.page - 1}" ${state.page <= 1 ? 'disabled' : ''}`)}<span class="quiet">Page ${state.page} of ${pages}</span>${button('Next →', 'page', `data-page="${state.page + 1}" ${state.page >= pages ? 'disabled' : ''}`)}</div>`;
}

async function collection() {
  const [categories, products] = await Promise.all([request('/api/categories', {anonymous: true}), request(`/api/products?${pageQuery(state.page, 12, {search: state.search, categoryId: state.category})}`, {anonymous: true})]);
  state.categories = categories;
  state.products = products.items;
  return `<section class="hero"><div class="hero-copy"><p class="eyebrow">Objects for a good everyday</p><h1>Everyday,<br><em>considered.</em></h1><p>Useful things. Thoughtfully chosen.<br>A collection that fits right into your life.</p><a href="#collection" class="text-button">Explore the collection ↓</a></div><div class="hero-art">${artwork('headphones')}<span class="stamp">LESS, BUT<br>BETTER.</span><span class="art-caption">Made for your everyday</span></div></section>
  <section class="section" id="collection"><div class="section-heading"><h2>The collection</h2><span class="quiet">${products.totalCount.toLocaleString()} considered essentials</span></div>
  <form class="filters" data-form="filters"><label class="screen-reader" for="search">Search products</label><input id="search" class="search" name="search" placeholder="Find your everyday…" maxlength="200" value="${e(state.search)}"><label class="screen-reader" for="category">Category</label><select name="category" id="category"><option value="">All categories</option>${categories.map(category => `<option value="${e(category.id)}" ${state.category === category.id ? 'selected' : ''}>${e(category.name)}</option>`).join('')}</select><button class="button secondary">Find goods</button></form>
  <div class="grid">${products.items.length ? products.items.map(product => `<article class="product"><div class="product-art">${artwork(product.name)}<span class="tag">Everyday essential</span><button type="button" class="add-button" data-action="add" data-id="${e(product.id)}" aria-label="Add ${e(product.name)} to bag">+</button></div><div class="product-meta"><div><h3>${e(product.name)}</h3><span class="category-label">${e(product.categoryName)}</span></div><span class="product-price">${money(product.price, product.currency)}</span></div></article>`).join('') : '<div class="empty"><h3>Nothing here just yet.</h3><p>Try another search or category.</p></div>'}</div>${pageControls(products.totalCount)}</section>
  <aside class="banner"><div><h2>A place for your everyday.</h2><p>Keep your favourites together. We’ll take care of the details.</p></div>${button('Open your bag ↗', 'cart-dialog', '', 'secondary')}</aside>`;
}

function signInPrompt(title) {
  return `<section class="section"><div class="page-heading"><h1>${title}</h1></div><div class="signin-prompt"><h2>A familiar place.</h2><p>Sign in to see your account and orders.</p>${button('Sign in', 'login-dialog', '', 'accent')}</div></section>`;
}

function orderRows(orders, admin = false) {
  return orders.map(order => `<tr><td><strong>${e(order.id.slice(0, 8))}</strong><br><span class="quiet">${date(order.createdAt)}</span></td><td>${badge(order.status)}</td><td>${order.items.length} items</td><td>${money(order.total, order.currency)}</td><td>${button('View', 'order-detail', `data-id="${e(order.id)}"`)}${admin && order.status === 'Confirmed' ? button('Ship', 'ship-dialog', `data-id="${e(order.id)}"`) : ''}${admin && order.status === 'Shipped' ? button('Deliver', 'deliver', `data-id="${e(order.id)}"`) : ''}</td></tr>`).join('');
}

async function ordersPage() {
  if (!state.auth) {
    return signInPrompt('Your orders.');
  }
  const result = await request(`/api/orders?${pageQuery(state.page, 20, {customerId: state.auth.user.id})}`);
  state.orders = result.items;
  return `<section class="section"><div class="page-heading"><p class="eyebrow">Your account</p><h1>Your orders.</h1><p>From your bag to your doorstep. ${result.totalCount.toLocaleString()} orders.</p></div>${result.items.length ? `<div class="table-wrap"><table><thead><tr><th>Order</th><th>Status</th><th>Items</th><th>Total</th><th>Details</th></tr></thead><tbody>${orderRows(result.items)}</tbody></table></div>${pageControls(result.totalCount, 20)}` : '<div class="empty"><h3>Your next good thing is waiting.</h3><p>Your first order will appear here.</p><a class="button secondary" href="#shop">Explore the collection</a></div>'}</section>`;
}

async function orderDetail(id) {
  const order = await request(`/api/orders/${encodeURIComponent(id)}`);
  openModal(`Order ${order.id.slice(0, 8)}`, `${badge(order.status)}<div class="detail-list">${order.items.map(item => `<div><span>${e(item.productName)} × ${item.quantity}</span><strong>${money(item.lineTotal, order.currency)}</strong></div>`).join('')}<div><span>Total</span><strong>${money(order.total, order.currency)}</strong></div></div><p class="form-note">${e(order.shippingAddress.recipient)}<br>${e(order.shippingAddress.line1)}, ${e(order.shippingAddress.city)} ${e(order.shippingAddress.postalCode)}<br>${e(order.shippingAddress.countryCode)}</p>${order.trackingNumber ? `<p class="form-note">Tracking: ${e(order.carrier)} · ${e(order.trackingNumber)}</p>` : ''}<div class="order-actions">${order.status === 'Reserved' ? button('Simulate payment', 'pay-dialog', `data-id="${e(order.id)}"`, 'accent') : ''}${['PendingInventory', 'Confirming', 'Cancelling'].includes(order.status) ? button('Retry processing', 'retry-order', `data-id="${e(order.id)}"`) : ''}${canCancel(order.status) ? button('Cancel order', 'cancel-dialog', `data-id="${e(order.id)}"`, 'danger') : ''}</div>`);
}

async function adminPage() {
  if (!state.auth) {
    return signInPrompt('The workspace.');
  }
  if (!isAdmin()) {
    return '<section class="section"><div class="page-heading"><h1>The workspace.</h1></div><div class="error-panel">An administrator account is required.</div></section>';
  }
  const tabs = ['products', 'categories', 'inventory', 'orders', 'access'];
  const header = `<section class="section"><div class="page-heading"><p class="eyebrow">Administrator workspace</p><h1>Behind the collection.</h1><p>Manage the goods, the stock, and everything on its way.</p></div><nav class="tabs" aria-label="Workspace sections">${tabs.map(tab => `<button type="button" data-action="admin-tab" data-tab="${tab}" class="${state.adminTab === tab ? 'active' : ''}">${tab[0].toUpperCase() + tab.slice(1)}</button>`).join('')}</nav>`;
  state.categories = await request('/api/categories');
  let content = '';
  if (state.adminTab === 'products') {
    const data = await request(`/api/products?${pageQuery(state.page, 20, {includeInactive: true})}`);
    state.products = data.items;
    content = `<div class="toolbar"><h2>Products <span class="quiet">${data.totalCount.toLocaleString()}</span></h2>${button('+ New product', 'product-dialog', '', 'accent')}</div><div class="table-wrap"><table><thead><tr><th>Product</th><th>Category</th><th>Price</th><th>State</th><th>Actions</th></tr></thead><tbody>${data.items.map(product => `<tr><td>${e(product.name)}</td><td>${e(product.categoryName)}</td><td>${money(product.price, product.currency)}</td><td>${badge(product.isActive ? 'Active' : 'Discontinued')}</td><td>${button('Edit', 'product-dialog', `data-id="${e(product.id)}"`)}${product.isActive ? button('Discontinue', 'discontinue-dialog', `data-id="${e(product.id)}"`, 'danger small') : ''}</td></tr>`).join('')}</tbody></table></div>${pageControls(data.totalCount, 20)}`;
  } else if (state.adminTab === 'categories') {
    content = `<div class="toolbar"><h2>Categories</h2>${button('+ New category', 'category-dialog', '', 'accent')}</div><div class="table-wrap"><table><thead><tr><th>Category</th><th>Description</th><th>Actions</th></tr></thead><tbody>${state.categories.map(category => `<tr><td>${e(category.name)}</td><td>${e(category.description || '—')}</td><td>${button('Edit', 'category-dialog', `data-id="${e(category.id)}"`)}${button('Delete', 'delete-category-dialog', `data-id="${e(category.id)}"`, 'danger small')}</td></tr>`).join('')}</tbody></table></div>`;
  } else if (state.adminTab === 'inventory') {
    const data = await request(`/api/inventory/stock?${pageQuery(state.page, 20)}`);
    state.inventory = data.items;
    content = `<div class="toolbar"><h2>Stock <span class="quiet">${data.totalCount.toLocaleString()} products</span></h2>${button('+ Create stock', 'stock-dialog', '', 'accent')}</div><div class="table-wrap"><table><thead><tr><th>Product ID</th><th>On hand</th><th>Reserved</th><th>Available</th><th>Actions</th></tr></thead><tbody>${data.items.map(stock => `<tr><td><code>${e(stock.productId)}</code></td><td>${stock.onHand}</td><td>${stock.reserved}</td><td>${stock.available}</td><td>${button('Adjust', 'stock-dialog', `data-id="${e(stock.productId)}"`)}</td></tr>`).join('')}</tbody></table></div>${pageControls(data.totalCount, 20)}`;
  } else if (state.adminTab === 'orders') {
    const data = await request(`/api/orders?${pageQuery(state.page, 20)}`);
    state.orders = data.items;
    content = `<div class="toolbar"><h2>All orders <span class="quiet">${data.totalCount.toLocaleString()}</span></h2>${button('Refresh', 'refresh')}</div><div class="table-wrap"><table><thead><tr><th>Order</th><th>Status</th><th>Items</th><th>Total</th><th>Actions</th></tr></thead><tbody>${orderRows(data.items, true)}</tbody></table></div>${pageControls(data.totalCount, 20)}`;
  } else {
    content = `<div class="toolbar"><h2>Administrator access</h2></div><p class="form-note">Use an exact account ID to update its administrator role. Customer access is retained. The server prevents removing your own administrator role.</p><form class="form" data-form="access">${field('User ID', 'id', '', 'required pattern="[a-fA-F0-9-]{36}"') }<label>Administrator role<select name="isAdmin"><option value="true">Grant administrator access</option><option value="false">Remove administrator access</option></select></label>${errorBlock}<button class="button accent">Update role</button></form>`;
  }
  return header + content + '</section>';
}

async function render() {
  const version = ++renderVersion;
  updateHeader();
  main.innerHTML = '<section class="section"><div class="loading" role="status">Gathering the good things…</div></section>';
  try {
    const html = route() === 'orders' ? await ordersPage() : route() === 'admin' ? await adminPage() : await collection();
    if (version === renderVersion) {
      main.innerHTML = html;
    }
  } catch (error) {
    if (version === renderVersion) {
      main.innerHTML = `<section class="section"><div class="page-heading"><h1>A moment, please.</h1></div><div class="error-panel">${e(error.message)}<p>Check that the API services are running, then try again.</p>${button('Try again', 'refresh')}</div></section>`;
    }
  }
}

function productDialog(id) {
  const product = state.products.find(product => product.id === id);
  openModal(product ? 'Edit product' : 'A new good thing.', `<form class="form" data-form="product" data-id="${e(id || '')}">${field('Product name', 'name', product?.name || '', 'required maxlength="200"')}<label>Description<textarea name="description" maxlength="2000">${e(product?.description || '')}</textarea></label><div class="form-row">${field('Price', 'price', product?.price ?? '', 'required type="number" min="0" max="9999999999999" step="0.01"')}${field('Currency', 'currency', product?.currency || 'USD', 'required pattern="[A-Z]{3}" maxlength="3"')}</div><label>Category<select name="categoryId" required><option value="">Choose a category</option>${state.categories.map(category => `<option value="${e(category.id)}" ${product?.categoryId === category.id ? 'selected' : ''}>${e(category.name)}</option>`).join('')}</select></label>${errorBlock}<button class="button accent">Save product</button><p class="form-note">New products need a stock record in Inventory before they can be ordered.</p></form>`);
}

function categoryDialog(id) {
  const category = state.categories.find(category => category.id === id);
  openModal(category ? 'Edit category' : 'New category', `<form class="form" data-form="category" data-id="${e(id || '')}">${field('Name', 'name', category?.name || '', 'required maxlength="150"')}<label>Description<textarea name="description" maxlength="1000">${e(category?.description || '')}</textarea></label>${errorBlock}<button class="button accent">Save category</button></form>`);
}

async function stockDialog(id, create = false) {
  if (id && !create) {
    try {
      await request(`/api/inventory/stock/${id}`);
    } catch (error) {
      if (error.status !== 404) {
        throw error;
      }
      create = true;
    }
  }
  const adjusting = id && !create;
  openModal(adjusting ? 'Adjust stock' : 'Create stock', `<form class="form" data-form="stock" data-id="${e(adjusting ? id : '')}">${adjusting ? `<p class="quiet">${e(id)}</p>` : field('Product ID', 'productId', id || '', 'required pattern="[a-fA-F0-9-]{36}"')}${field(adjusting ? 'Quantity change (positive or negative)' : 'Initial quantity', 'quantity', '', `type="number" required step="1" ${adjusting ? '' : 'min="0"'}`)}${adjusting ? field('Reason', 'reason', '', 'required maxlength="500"') : ''}${errorBlock}<button class="button accent">Save stock</button></form>`);
}

function confirmation(title, message, form, id, label) {
  openModal(title, `<p class="form-note">${e(message)}</p><form class="form" data-form="${form}" data-id="${e(id)}">${errorBlock}<button class="button danger">${label}</button></form>`);
}

async function handleAction(element) {
  const {action, id} = element.dataset;
  if (action === 'close') {
    modal.close();
  } else if (action === 'login-dialog' || action === 'register-dialog') {
    authDialog(action === 'register-dialog');
  } else if (action === 'cart-dialog') {
    await cartDialog();
  } else if (action === 'checkout-dialog') {
    checkoutDialog();
  } else if (action === 'add') {
    if (!state.auth) {
      authDialog();
      return;
    }
    await refreshCart();
    const quantity = (state.cart.items.find(item => item.productId === id)?.quantity || 0) + 1;
    await request(`/api/cart/items/${id}`, {method: 'PUT', body: {quantity}});
    await refreshCart();
    notify('A good addition. Saved to your bag.');
  } else if (action === 'quantity') {
    const quantity = Number(element.dataset.quantity);
    await request(`/api/cart/items/${id}`, {method: quantity ? 'PUT' : 'DELETE', body: quantity ? {quantity} : undefined});
    state.checkout = null;
    await cartDialog();
  } else if (action === 'page') {
    state.page = Number(element.dataset.page);
    await render();
    document.querySelector('#collection')?.scrollIntoView();
  } else if (action === 'admin-tab') {
    state.adminTab = element.dataset.tab;
    state.page = 1;
    await render();
  } else if (action === 'refresh') {
    await render();
  } else if (action === 'go-orders') {
    modal.close();
    location.hash = 'orders';
    await render();
  } else if (action === 'order-detail') {
    await orderDetail(id);
  } else if (action === 'pay-dialog') {
    paymentDialog(await request(`/api/orders/${id}`));
  } else if (action === 'retry-order') {
    await request(`/api/orders/${id}/retry`, {method: 'POST'});
    await orderDetail(id);
    await render();
  } else if (action === 'retry-checkout') {
    const order = await request('/api/cart/checkout/retry', {method: 'POST'});
    await refreshCart();
    paymentDialog(order);
  } else if (action === 'product-dialog') {
    productDialog(id);
  } else if (action === 'category-dialog') {
    categoryDialog(id);
  } else if (action === 'stock-dialog') {
    await stockDialog(id);
  } else if (action === 'discontinue-dialog') {
    confirmation('Discontinue product?', 'It will leave the active collection. Historical order records are preserved.', 'discontinue', id, 'Discontinue');
  } else if (action === 'delete-category-dialog') {
    confirmation('Delete category?', 'Only categories without products can be deleted.', 'delete-category', id, 'Delete category');
  } else if (action === 'cancel-dialog') {
    openModal('Cancel order?', `<form class="form" data-form="cancel-order" data-id="${e(id)}">${field('Reason', 'reason', '', 'required maxlength="500"')}${errorBlock}<p class="form-note">Reserved stock is released. Paid orders request a simulated refund.</p><button class="button danger">Cancel order</button></form>`);
  } else if (action === 'ship-dialog') {
    openModal('Ship order', `<form class="form" data-form="ship" data-id="${e(id)}">${field('Carrier', 'carrier', '', 'required maxlength="100"')}${field('Tracking number', 'trackingNumber', '', 'required maxlength="150"')}${errorBlock}<button class="button accent">Mark shipped</button></form>`);
  } else if (action === 'deliver') {
    confirmation('Mark delivered?', 'Confirm that this shipped order reached the customer.', 'deliver', id, 'Mark delivered');
  }
}

async function handleForm(form) {
  const values = Object.fromEntries(new FormData(form));
  const kind = form.dataset.form;
  const id = form.dataset.id;
  if (kind === 'filters') {
    state.search = values.search.trim();
    state.category = values.category;
    state.page = 1;
    await render();
    return;
  }
  if (kind === 'login' || kind === 'register') {
    state.auth = await request(`/api/identity/${kind}`, {method: 'POST', body: values, anonymous: true});
    form.reset();
    await refreshCart();
    modal.close();
    notify(`Welcome, ${state.auth.user.displayName}.`);
    await render();
    return;
  }
  if (kind === 'checkout') {
    const address = {...values, line2: values.line2 || null, region: values.region || null, countryCode: values.countryCode.toUpperCase()};
    if (!state.checkout || JSON.stringify(state.checkout.shippingAddress) !== JSON.stringify(address)) {
      state.checkout = {requestId: crypto.randomUUID(), shippingAddress: address};
    }
    const order = await request('/api/cart/checkout', {method: 'POST', body: state.checkout});
    state.checkout = null;
    await refreshCart();
    paymentDialog(order);
    return;
  }
  if (kind === 'payment') {
    const key = `${id}:${values.methodToken}`;
    if (!paymentIds.has(key)) {
      paymentIds.set(key, crypto.randomUUID());
    }
    const payment = await request('/api/payments', {method: 'POST', body: {requestId: paymentIds.get(key), orderId: id, methodToken: values.methodToken}});
    openModal(payment.orderNotified ? 'A good thing is on its way.' : 'Payment is processing.', `<div class="success-symbol">✓</div><p class="form-note">${payment.orderNotified ? 'Your simulated payment was approved and your order confirmed.' : 'Your payment is recorded. Check your orders shortly for confirmation.'}</p>${button('View my orders →', 'go-orders', '', 'accent full')}`);
    await render();
    return;
  }
  if (kind === 'product') {
    const created = await request(`/api/products${id ? '/' + id : ''}`, {method: id ? 'PUT' : 'POST', body: {...values, price: Number(values.price), description: values.description || null}});
    if (!id) {
      await render();
      notify('Product created. Add its initial stock to make it orderable.');
      await stockDialog(created.id, true);
      return;
    }
  } else if (kind === 'category') {
    await request(`/api/categories${id ? '/' + id : ''}`, {method: id ? 'PUT' : 'POST', body: {...values, description: values.description || null}});
  } else if (kind === 'discontinue') {
    await request(`/api/products/${id}`, {method: 'DELETE'});
  } else if (kind === 'delete-category') {
    await request(`/api/categories/${id}`, {method: 'DELETE'});
  } else if (kind === 'stock') {
    await request(id ? `/api/inventory/stock/${id}/adjustments` : '/api/inventory/stock', {method: 'POST', body: id ? {requestId: crypto.randomUUID(), delta: Number(values.quantity), reason: values.reason} : {productId: values.productId, quantity: Number(values.quantity)}});
  } else if (kind === 'cancel-order') {
    await request(`/api/orders/${id}/cancel`, {method: 'POST', body: values});
  } else if (kind === 'ship' || kind === 'deliver') {
    await request(`/api/orders/${id}/${kind}`, {method: 'POST', body: kind === 'ship' ? values : undefined});
  } else if (kind === 'access') {
    await request(`/api/identity/users/${encodeURIComponent(values.id)}/admin`, {method: 'PUT', body: {isAdmin: values.isAdmin === 'true'}});
  }
  modal.close();
  notify('Saved. Everything is up to date.');
  await render();
}

document.addEventListener('click', async event => {
  const element = event.target.closest('[data-action]');
  if (!element || element.disabled) {
    return;
  }
  element.disabled = true;
  try {
    await handleAction(element);
  } catch (error) {
    notify(error.message, true);
  } finally {
    element.disabled = false;
  }
});

document.addEventListener('submit', async event => {
  const form = event.target.closest('[data-form]');
  if (!form) {
    return;
  }
  event.preventDefault();
  if (form.dataset.busy) {
    return;
  }
  form.dataset.busy = 'true';
  const submit = form.querySelector('button:not([type="button"])');
  const errorElement = form.querySelector('.form-error');
  if (submit) {
    submit.disabled = true;
  }
  if (errorElement) {
    errorElement.hidden = true;
  }
  try {
    await handleForm(form);
  } catch (error) {
    if (errorElement) {
      errorElement.textContent = error.message;
      errorElement.hidden = false;
    } else {
      notify(error.message, true);
    }
  } finally {
    delete form.dataset.busy;
    if (submit) {
      submit.disabled = false;
    }
  }
});

document.querySelector('#cart-button').addEventListener('click', () => cartDialog().catch(error => notify(error.message, true)));
document.querySelector('#account-button').addEventListener('click', async () => {
  if (!state.auth) {
    authDialog();
    return;
  }
  try {
    await request('/api/identity/logout', {method: 'POST', body: {refreshToken: state.auth.refreshToken}});
  } catch {
    notify('Signed out locally. The server could not revoke the refresh session.', true);
  } finally {
    state.auth = null;
    state.cart = null;
    state.checkout = null;
    paymentIds.clear();
    updateHeader();
    await render();
  }
});

window.addEventListener('hashchange', () => {
  if (location.hash === '#collection') {
    return;
  }
  state.page = 1;
  render();
});

render();
