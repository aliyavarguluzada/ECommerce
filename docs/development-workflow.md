# Storefront, API verification, and CI

## Start the application

The gateway now serves a responsive storefront and administrator workspace at
[http://localhost:5100/shop/](http://localhost:5100/shop/). It uses the real APIs
through the gateway on the same origin; no Node server or frontend build is needed.

To use the large seed databases from WSL, run:

```bash
cd /mnt/c/Users/aliya/Desktop/work/ECommerce
docker compose -f docker-compose.yml -f docker-compose.seed.yml up -d --build
```

The storefront includes product search/category filtering/paging, registration and
sign-in, cart quantity changes, shipping details, simulated payment, order history,
order details, pending checkout recovery, and cancellation. The administrator
workspace adds product/category management, stock creation/adjustments, all-customer
order browsing, shipment/delivery, and administrator-role updates by user ID.
The APIs enforce all permissions, independent of whether a control is shown.

Use `user1@example.test` for administration or `user2@example.test` through
`user10@example.test` for customer testing. The password is in the Git-ignored
`.tools/seed-demo-login.txt`. Alternatively, register a new customer in the UI.
After a new product is saved, its initial-stock form opens automatically with the
product ID filled in. Existing stock adjustments require a reason. The Inventory
tab also supports creating a stock record by product ID.

Bearer and refresh tokens are held in browser memory, not local/session storage.
Reloading the page requires sign-in again. Expired access tokens use the normal
refresh endpoint; sign-out revokes the refresh session when the server is reachable.
The gateway serves a restrictive content-security policy for the storefront, and
product/account values are escaped before insertion into HTML.

Payments remain development simulations. There are no real card-entry fields,
charges, shipping integrations, taxes, or external payment-provider calls.

## Use authenticated Swagger

Open [http://localhost:5100/swagger](http://localhost:5100/swagger) and choose an
API from the selector. The gateway fetches its document server-side and makes
"Try it out" use the gateway origin, so no cross-origin browser setup is needed.
Internal/service-key operations are removed from these public gateway documents.

1. Choose Identity, expand `POST /api/identity/login`, and submit the demo email/password.
2. Copy `accessToken` from the response.
3. Choose the desired API and click **Authorize**. Paste the token without `Bearer `.
4. Execute an authorized operation, such as `GET /api/cart` or `GET /api/orders`.

Public catalog reads and identity login/registration remain anonymous in the
documents. Protected public operations declare HTTP bearer authentication.
Direct service Swagger pages also document the `X-Service-Key` scheme for internal
operations; a customer JWT is not a replacement for that service credential.
Swagger and documentation proxy endpoints are enabled only in Development.

## Verify a complete checkout through real services

Node 22 or newer is sufficient; no npm dependencies are needed. Start the seed
stack first, then run from the repository root:

```powershell
$env:ECOMMERCE_WRITE_SMOKE = '1'
node scripts/verify-checkout.mjs
Remove-Item Env:ECOMMERCE_WRITE_SMOKE
```

In WSL (with Node installed):

```bash
ECOMMERCE_WRITE_SMOKE=1 node scripts/verify-checkout.mjs
```

By default the scripts call `http://localhost:5100`, and read only the admin password
from the ignored demo-credentials file. To use another local configuration, set
`ECOMMERCE_BASE_URL`, `ECOMMERCE_ADMIN_EMAIL`, and `ECOMMERCE_ADMIN_PASSWORD`.
Only loopback targets are accepted. Passwords and tokens are not included in reports.

The verifier creates a unique `Smoke <run-id>` category, two products with stock,
and one customer, then verifies:

- Anonymous cart requests fail with 401.
- Checkout reserves exactly two units and reusing its request ID returns the same order.
- Simulated payment confirms the order; replay does not charge or consume stock twice.
- Customer fulfillment requests fail with 403; an admin can ship and deliver.
- Declined payment returns 402; cancellation restores the reservation balance.
- Eight simultaneous orders requesting four units each against stock of five produce
  one reservation; the other seven remain pending until explicitly cancelled.

On completion/failure the verifier attempts to cancel outstanding orders, discontinues
its two products, and revokes its sessions. It retains labelled historical records
rather than deleting orders/payments. Results are written to `artifacts/checkout-report.json`.
**This deliberately adds data to the target seed databases.** The strict seed-tool
baseline check will report differences after interactive tests or this verifier;
it does not erase those changes or restore baseline counts. Keep original seed
verification reports if you need a fixed-volume baseline.

The complete script passed locally, including the concurrency and cleanup checks.
Browser QA also verified search, admin sign-in, workspace data, cart/shipping,
simulated payment confirmation, and the updated customer order history.

## Performance checks

Run read-only HTTP measurements against the seeded stack:

```bash
node scripts/benchmark-api.mjs
```

The script warms each route twice, then measures 20 requests with concurrency four.
It checks that each response is successful, paged, and populated. The default
gateway rate limit stays enabled; avoid running multiple benchmark copies in the
same minute. Results are written to `artifacts/performance-report.json`.

Local baseline on 2026-10-18 with roughly 100,000 users/orders and 1,000 products:

| Scenario | Median | p95 |
| --- | ---: | ---: |
| Catalog paging | 8.7 ms | 12.9 ms |
| Literal catalog search | 8.0 ms | 22.4 ms |
| Customer order history | 11.0 ms | 17.5 ms |
| Deep order paging (including page 5,000) | 73.8 ms | 88.0 ms |
| Inventory paging | 6.5 ms | 13.4 ms |

These are small, warm local HTTP samples, not production capacity or latency
guarantees. Deep offset paging was the slowest measured operation; cursor-based
paging is the next candidate if datasets or latency requirements grow. There is
no evidence from this run requiring a speculative database-index change.

## Automated verification and GitHub Actions

The workflow in `.github/workflows/ci.yml` runs on pull requests, pushes to
`main`/`master`, and manual dispatch. It starts PostgreSQL 18 with an ephemeral
CI-only database, restores/builds the full solution in Release, runs all .NET tests,
then checks the frontend module and runs the Node unit tests. TRX results are uploaded
even on failure. No development authentication files or repository secrets are required.

Local verification: **161 .NET tests passed, zero skipped**, including six live
PostgreSQL tests, plus **six browser-module tests passed**. The PostgreSQL test
database used for this verification was removed afterward. A hosted GitHub Actions
run begins only after the workflow is committed and pushed; it has not run remotely yet.

```powershell
dotnet test tests/ECommerce.Tests/ECommerce.Tests.csproj '-p:Platform=AnyCPU'
node --check src/ECommerce.ApiGateway/wwwroot/shop/app.js
node --test tests/frontend/commerce.test.mjs
```

Without `ECOMMERCE_TEST_POSTGRES_CONNECTION`, the six database tests are still
explicitly skipped locally; CI always sets it.
