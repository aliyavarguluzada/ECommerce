# Large demonstration dataset

The seed tool creates six separate `ecommerce_seed_*` databases on the existing
PostgreSQL server. It never truncates, drops, migrates, or seeds the normal
`catalog_db`, `identity_db`, `order_db`, `payment_db`, `inventory_db`, or `cart_db`.
The running API containers keep using those databases until you explicitly apply
the optional Compose override below.

## Dataset

| Database | Application table | Rows |
| --- | --- | ---: |
| ecommerce_seed_catalog | categories | 50 |
| ecommerce_seed_catalog | products | 1,000 |
| ecommerce_seed_identity | AspNetUsers | 100,000 |
| ecommerce_seed_identity | AspNetRoles | 2 |
| ecommerce_seed_identity | AspNetUserRoles | 100,001 |
| ecommerce_seed_identity | AspNetRoleClaims | 2 |
| ecommerce_seed_identity | AspNetUserClaims | 100,000 |
| ecommerce_seed_identity | AspNetUserLogins | 1,000 |
| ecommerce_seed_identity | AspNetUserTokens | 1,000 |
| ecommerce_seed_identity | refresh_sessions | 1,000 |
| ecommerce_seed_order | orders | 100,000 |
| ecommerce_seed_order | order_items | 300,000 |
| ecommerce_seed_payment | payments | 95,000 |
| ecommerce_seed_inventory | stock_items | 1,000 |
| ecommerce_seed_inventory | stock_adjustments | 1,000 |
| ecommerce_seed_inventory | reservations | 100,000 |
| ecommerce_seed_inventory | reservation_items | 300,000 |
| ecommerce_seed_cart | carts | 20,000 |
| ecommerce_seed_cart | cart_items | 60,000 |
| ecommerce_seed_cart | checkout_attempts | 100,000 |
| ecommerce_seed_cart | checkout_items | 300,000 |

All amounts use USD. Every order has three distinct products. The distribution is
10,000 confirmed, 20,000 shipped, 60,000 delivered, and 10,000 cancelled orders.
There are 90,000 successful payments and 5,000 refunded payments; 5,000 cancelled
orders were never paid. Every reservation is committed or released, every checkout
is completed, and payment notifications are already acknowledged. Thus historical
rows do not enter recovery workflows when the seed APIs start.

Stock begins with 10,000 units per product. Committed order quantities are deducted
from current stock, released reservations consume no stock, and no units remain
reserved. Carts contain new items independent of their historical completed checkouts.
Names, emails (`example.test`), addresses, carrier references, and prices are synthetic.
IDs are deterministic under the versioned `ecommerce-seed-v1` dataset identifier.

## Demo sign-in

The first ten users have independently salted ASP.NET Identity password hashes.
`user1@example.test` has both Admin and Customer roles; `user2@example.test` through
`user10@example.test` are customers. A random shared demo password is written to
the Git-ignored `.tools/seed-demo-login.txt`. It is never printed in the seed report.
Set `SEED_DEMO_PASSWORD` before the first run to choose your own demo password.

The other 99,990 users have no password hash and cannot sign in with a password.
This avoids generating 100,000 costly password hashes just to test data volume.
Use those accounts for SQL/load testing, or assign a password through a dedicated
development workflow if you need more interactive accounts.

Refresh sessions are expired and revoked. External login/token rows use an inert
`SeedOnly` provider with marker values, not real provider credentials or active tokens.

## Run or verify again

Start PostgreSQL first. Run from the repository root with the .NET 10 SDK:

```powershell
$env:SEED_POSTGRES_CONNECTION = 'Host=localhost;Port=5432;Database=postgres;Username=ecommerce;Password=ecommerce'
.\scripts\Seed-Development.ps1
Remove-Item Env:SEED_POSTGRES_CONNECTION
```

Or in WSL with the .NET 10 SDK installed:

```bash
export SEED_POSTGRES_CONNECTION='Host=localhost;Port=5432;Database=postgres;Username=ecommerce;Password=ecommerce'
dotnet run --project tools/ECommerce.Seed/ECommerce.Seed.csproj -p:Platform=AnyCPU
unset SEED_POSTGRES_CONNECTION
```

Connection credentials above match the local Compose defaults. The database role
must be able to create databases. The seed tool only targets its fixed six database
names, rejects nonempty databases without its version manifest, and takes a run lock
to prevent concurrent runs. SQL inserts run in one transaction per service database.
Failed service inserts roll back; already completed service databases are verified
and reused on the next run. Rerunning does not duplicate data, rotate demo passwords,
or reset changes: unexpected counts or relationships cause verification to fail.

Each run writes `artifacts/seed-report.json` with exact per-table counts, database
sizes after temporary verification tables are dropped, duration, and one query's
`EXPLAIN ANALYZE` execution time per database. These single local measurements are
smoke checks, not concurrent load benchmarks or cold-cache performance guarantees.
Validation compares generated IDs, order totals, payment ownership/status/amounts,
stock conservation, reservations, checkout snapshots, and foreign keys. EF also
loads sample entities and recomputes order/checkout replay hashes using the actual
.NET serialization conventions.

## Inspect in DataGrip

Use host `localhost`, port `5432`, user `ecommerce`, password `ecommerce` (the local
Compose defaults), and select the six `ecommerce_seed_*` databases in the data source.
Each database contains the actual EF migration history and a `seed_manifest` row.

## Optionally point the APIs at the seed data

This command explicitly reconfigures the API containers to use the seed databases:

```bash
docker compose -f docker-compose.yml -f docker-compose.seed.yml up -d --build
```

Sign in through Identity using a demo account to exercise the seeded customer/admin
APIs. To switch the API containers back to the ordinary development databases:

```bash
docker compose -f docker-compose.yml up -d
```

Neither command deletes either set of databases.
