import assert from 'node:assert/strict';
import {performance} from 'node:perf_hooks';
import {createApi, adminSession, saveReport} from './lib/http.mjs';

const api = createApi();
const admin = await adminSession(api);
const samples = 20;
const concurrency = 4;
const cases = [
  {name: 'catalog paging', path: index => `/api/products?pageNumber=${index % 10 + 1}&pageSize=20`},
  {name: 'catalog literal search', path: index => `/api/products?search=${index % 2 ? 'Headphones' : 'Model'}&pageSize=20`},
  {name: 'customer order history', path: () => `/api/orders?customerId=${admin.user.id}&pageSize=20`},
  {name: 'deep order pagination', path: index => `/api/orders?pageNumber=${[1, 2500, 5000][index % 3]}&pageSize=20`},
  {name: 'inventory paging', path: index => `/api/inventory/stock?pageNumber=${index % 10 + 1}&pageSize=20`}
];
const results = [];
try {
  for (const scenario of cases) {
    for (let index = 0; index < 2; index++) {
      await api(scenario.path(index), {token: admin.accessToken});
    }
    const durations = [];
    let next = 0;
    const started = performance.now();
    await Promise.all(Array.from({length: concurrency}, async () => {
      while (next < samples) {
        const index = next++;
        const begin = performance.now();
        const {data} = await api(scenario.path(index), {token: admin.accessToken});
        assert.ok(Array.isArray(data.items));
        assert.ok(data.totalCount > 0, 'Benchmark requires populated seed data.');
        durations.push(performance.now() - begin);
      }
    }));
    const elapsed = performance.now() - started;
    durations.sort((a, b) => a - b);
    const result = {scenario: scenario.name, samples, concurrency, medianMs: durations[Math.floor(samples / 2)], p95Ms: durations[Math.ceil(samples * .95) - 1], maxMs: durations.at(-1), requestsPerSecond: samples / (elapsed / 1000)};
    results.push(result);
    console.log(`${scenario.name}: median ${result.medianMs.toFixed(1)}ms; p95 ${result.p95Ms.toFixed(1)}ms`);
  }
  await saveReport('performance-report.json', {result: 'passed', scope: 'Local warm HTTP measurements; not a production capacity claim.', results});
} finally {
  await api('/api/identity/logout', {method: 'POST', token: admin.accessToken, body: {refreshToken: admin.refreshToken}});
}
