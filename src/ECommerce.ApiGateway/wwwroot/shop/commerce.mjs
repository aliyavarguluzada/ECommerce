export function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, character => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[character]));
}

export function money(value, currency = 'USD') {
  return new Intl.NumberFormat('en-US', {style: 'currency', currency}).format(Number(value ?? 0));
}

export function problemMessage(body, fallback = 'The request could not be completed.') {
  if (body?.errors) {
    return Object.values(body.errors).flat().join(' ');
  }
  return body?.detail || body?.title || fallback;
}

export function canCancel(status) {
  return ['PendingInventory', 'Reserved', 'Confirmed'].includes(status);
}

export function pageQuery(pageNumber, pageSize, values = {}) {
  const query = new URLSearchParams({pageNumber, pageSize});
  for (const [key, value] of Object.entries(values)) {
    if (value !== '' && value !== null && value !== undefined) {
      query.set(key, value);
    }
  }
  return query;
}

export function artwork(name = '') {
  const paths = {
    headphones:'<path d="M58 105V83a42 42 0 0 1 84 0v22" fill="none" stroke="currentColor" stroke-width="12"/><rect x="45" y="94" width="26" height="58" rx="12" fill="currentColor"/><rect x="129" y="94" width="26" height="58" rx="12" fill="currentColor"/><path d="M73 59q27-24 54 0" fill="none" stroke="#fff" stroke-opacity=".28" stroke-width="4"/>',
    keyboard:'<rect x="22" y="63" width="156" height="80" rx="9" fill="currentColor"/><path d="M37 81h126M37 99h126M37 117h126" stroke="#fff" stroke-opacity=".55" stroke-width="8" stroke-dasharray="9 7"/><path d="M71 133h60" stroke="#fff" stroke-opacity=".55" stroke-width="6"/>',
    lamp:'<path d="M60 90h80L121 39H80Z" fill="currentColor"/><path d="M100 90v70" stroke="currentColor" stroke-width="8"/><ellipse cx="100" cy="164" rx="33" ry="8" fill="currentColor"/><path d="M71 83h58" stroke="#fff" stroke-opacity=".2" stroke-width="4"/>',
    backpack:'<path d="M85 48v-7a15 15 0 0 1 30 0v7" fill="none" stroke="currentColor" stroke-width="6"/><rect x="56" y="46" width="88" height="123" rx="30" fill="currentColor"/><rect x="68" y="111" width="64" height="43" rx="10" fill="#fff" fill-opacity=".17"/><path d="M76 126h48M69 85q31-30 62 0" stroke="#fff" stroke-opacity=".4" fill="none" stroke-width="3"/>',
    watch:'<rect x="84" y="23" width="32" height="154" rx="11" fill="currentColor"/><circle cx="100" cy="100" r="37" fill="#dbd5c7" stroke="currentColor" stroke-width="8"/><path d="M100 77v24l17 10" fill="none" stroke="currentColor" stroke-width="5" stroke-linecap="round"/>',
    chair:'<path d="M61 95V56a39 39 0 0 1 78 0v39Z" fill="currentColor"/><rect x="53" y="99" width="94" height="23" rx="9" fill="currentColor"/><path d="M68 120l-9 53M132 120l9 53" stroke="currentColor" stroke-width="8"/>',
    notebook:'<rect x="53" y="32" width="94" height="136" rx="5" fill="currentColor"/><path d="M67 34v132M80 74h49M80 84h37" stroke="#fff" stroke-opacity=".4" stroke-width="3"/><path d="M135 35v129" stroke="#fff" stroke-opacity=".5" stroke-width="4"/>',
    bottle:'<rect x="84" y="24" width="32" height="21" rx="5" fill="currentColor"/><path d="M81 44h38v18l12 16v80a14 14 0 0 1-14 14H83a14 14 0 0 1-14-14V78l12-16Z" fill="currentColor"/><path d="M81 91v57" stroke="#fff" stroke-opacity=".25" stroke-width="5" stroke-linecap="round"/>',
    speaker:'<rect x="57" y="32" width="86" height="138" rx="16" fill="currentColor"/><circle cx="100" cy="121" r="28" fill="#fff" fill-opacity=".15"/><circle cx="100" cy="121" r="13" fill="currentColor"/><circle cx="100" cy="64" r="13" fill="#fff" fill-opacity=".3"/>',
    jacket:'<path d="M74 39l26 9 26-9 36 28-15 48-20-9v63H73v-63l-20 9-15-48Z" fill="currentColor"/><path d="M100 48v119M74 39l11 33 15-24 15 24 11-33M78 123h15M107 123h15" stroke="#fff" stroke-opacity=".35" fill="none" stroke-width="3"/>'
  };
  const key = Object.keys(paths).find(key => String(name || '').toLowerCase().includes(key)) || 'backpack';
  return `<svg viewBox="0 0 200 200" aria-hidden="true">${paths[key]}</svg>`;
}
