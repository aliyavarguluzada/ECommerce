import {readFile, mkdir, writeFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';

export const root = fileURLToPath(new URL('../../', import.meta.url));

export function createApi() {
  const base = new URL(process.env.ECOMMERCE_BASE_URL || 'http://localhost:5100');
  if (!['localhost', '127.0.0.1', '[::1]'].includes(base.hostname)) {
    throw new Error('This development verifier only accepts a loopback gateway URL.');
  }
  return async (path, {method = 'GET', body, token, allow = []} = {}) => {
    const response = await fetch(new URL(path, base), {
      method,
      signal: AbortSignal.timeout(30000),
      headers: {...(body === undefined ? {} : {'Content-Type': 'application/json'}), ...(token ? {Authorization: `Bearer ${token}`} : {})},
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    const text = await response.text();
    let data;
    try {
      data = text ? JSON.parse(text) : null;
    } catch {
      throw new Error(`${method} ${path}: unexpected non-JSON response (${response.status}).`);
    }
    if (!response.ok && !allow.includes(response.status)) {
      throw new Error(`${method} ${path}: ${response.status} ${data?.detail || data?.title || 'request failed'}`);
    }
    return {status: response.status, data};
  };
}

export async function adminSession(api) {
  let password = process.env.ECOMMERCE_ADMIN_PASSWORD;
  if (!password) {
    const text = await readFile(resolve(root, '.tools/seed-demo-login.txt'), 'utf8');
    password = text.match(/^Password: (.+)$/m)?.[1]?.trim();
  }
  if (!password) {
    throw new Error('Set ECOMMERCE_ADMIN_PASSWORD or create the demo accounts with the seed tool.');
  }
  const {data} = await api('/api/identity/login', {method: 'POST', body: {email: process.env.ECOMMERCE_ADMIN_EMAIL || 'user1@example.test', password}});
  if (!data.user.roles.includes('Admin')) {
    throw new Error('The verifier needs an administrator account.');
  }
  return data;
}

export async function saveReport(name, data) {
  const directory = resolve(root, 'artifacts');
  await mkdir(directory, {recursive: true});
  const path = resolve(directory, name);
  await writeFile(path, JSON.stringify({verifiedAtUtc: new Date().toISOString(), ...data}, null, 2) + '\n');
  console.log(`Report: ${path}`);
}
