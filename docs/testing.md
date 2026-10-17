# Automated tests

Run from the solution root with the .NET 10 SDK:

```powershell
dotnet test tests/ECommerce.Tests/ECommerce.Tests.csproj '-p:Platform=AnyCPU'
```

The xUnit suite covers Catalog, Inventory, Order, Identity, Cart, Payment, the
API gateway, and their shared authentication. No running APIs, local security
files, message broker, or payment account are needed for the default suite.
Tests generate temporary RSA signing keys and service credentials inside the test
process. Parallel execution is disabled because these credentials use process
environment variables and application tests share transactional test stores.

| Area | Coverage |
| --- | --- |
| Catalog | Category/product HTTP CRUD, conflicts, paging, filtering, validation, lifecycle, domain invariants, provider error mapping, SQL query translation |
| Inventory | Stock bounds, atomic bundle rollback, reservation commit/release, release tombstones, adjustment replay, pagination, administrator/service authorization |
| Order | Catalog snapshots, currency validation, request replay, lost reservation responses, payment matching, expiry, cancellation/refund recovery, fulfillment, ownership |
| Identity | JWT signature/issuer/audience/expiry/subject rejection, password policy, request validation, profile claims, role permissions; database tests cover account and refresh-session workflows |
| Cart | Live price quotes, unavailable/mixed-currency products, quantities, frozen checkout recovery, address conflicts, replay preserving new items, ownership |
| Payment | Approval/decline, replay, ownership, callback recovery, late-payment compensation, refund recovery, development-provider restrictions |
| Gateway | Route policies, decoded internal-route blocking, bearer forwarding, removal of spoofable user/service headers |
| Integration | Cart checkout through real HTTP clients to Order and Inventory, Payment callbacks to Order, shipment/delivery and decline/cancellation |
| Persistence | PostgreSQL model/migration consistency and script generation for all six databases; optional live tests described below |

Application tests use the production services with transactional repository doubles.
The doubles copy state at transaction entry and publish it on commit, so tests can
assert rollback behavior. They do not prove PostgreSQL locking behavior. API tests
use `WebApplicationFactory` and the production authentication, controllers, serializers,
and exception handlers. Identity HTTP policy tests substitute an explicit stub;
account persistence and token rotation are tested against PostgreSQL separately.
Gateway tests use the real YARP pipeline with a recording upstream transport.

The cross-service checkout tests use actual HTTP service clients and internal service
credentials between ASP.NET test servers. Storage remains in memory in these tests;
Catalog data and the payment provider use deterministic test doubles. Recovery is
invoked directly, with background recovery timers disabled to avoid timing races.
Deployment/Compose and timed worker scheduling are outside this suite.

## Live PostgreSQL tests

Six tests are explicitly skipped when `ECOMMERCE_TEST_POSTGRES_CONNECTION` is absent.
Provide a connection to a dedicated test database on PostgreSQL 16 or newer. Its user
must be able to create/drop schemas and create tables. Tests apply the actual EF
migrations to randomly named `ecommerce_tests_<guid>` schemas, use only that schema
as the search path, and drop their own schema on disposal. Pooling is disabled for
these connections. No public service tables are migrated or deleted.

```powershell
$env:ECOMMERCE_TEST_POSTGRES_CONNECTION = 'Host=localhost;Port=5432;Database=ecommerce_tests;Username=ecommerce;Password=<test-password>'
dotnet test tests/ECommerce.Tests/ECommerce.Tests.csproj '-p:Platform=AnyCPU'
# Run only database tests:
dotnet test tests/ECommerce.Tests/ECommerce.Tests.csproj '-p:Platform=AnyCPU' --filter 'Category=PostgreSQL'
Remove-Item Env:ECOMMERCE_TEST_POSTGRES_CONNECTION
```

A configured but unreachable database fails these tests instead of silently skipping.
The live tests verify Catalog optimistic concurrency and owned Money persistence;
competing Inventory reservations; Order snapshots and replay after a new context;
Cart checkout persistence; Payment receipt/refund replay after a new context; and
Identity registration, duplicate accounts, refresh rotation/reuse revocation, logout,
password changes, role-change revocation, self-demotion rejection, and lockout.
Other services and the payment provider remain test doubles in each database test.

Latest verification (2026-10-18): **161 passed, 0 skipped, 0 failed**, with all six
live tests running in a fresh temporary database on PostgreSQL 18. The temporary
test database was removed afterward. Without a configured
connection, the default suite still skips the six live tests.

Nine additional tests cover bearer/service-key OpenAPI metadata, public endpoint
documentation, the gateway documentation proxy, and storefront delivery/security headers.
Six Node tests cover browser rendering helpers and input encoding. See
[Development workflow](development-workflow.md) for the full checkout verifier,
performance baseline, and GitHub Actions setup.

## Existing Catalog checks

The older executable remains available, with its write fixtures updated to use
temporary administrator credentials:

```powershell
dotnet run --project tests/ECommerce.Catalog.Checks '-p:Platform=AnyCPU'
```

Its optional live checks still use `CATALOG_TEST_CONNECTION_STRING`. The standard
xUnit suite also runs its category/product HTTP, query, and provider-mapping checks.
