# Inventory and Order

## Implemented scope

Both services use separate Domain, Application, Infrastructure, and API projects.
They own inventory_db and order_db respectively. They communicate with Catalog
and each other over HTTP; neither references another service's domain or database.

Inventory owns stock balances, adjustments, reservations, commitment, and release.
Order owns customer/address data, immutable product/price snapshots, order totals,
reservation coordination, confirmation, cancellation, shipping, and delivery.
Identity and development Payment are now integrated; see
[Identity, Cart, Payment, and Gateway](identity-cart-payment-gateway.md).
Tax, discounts, shipping charges, returns, and real payment processing remain separate.

Automated application and HTTP integration tests now cover these services; see
[Automated tests](testing.md). Live PostgreSQL and container runs remain unverified
locally. Migrations have been generated, not applied to a running database here.

## Inventory behavior

Stock is tracked per product with OnHand, Reserved, and Available = OnHand - Reserved.
Initialize stock only for an active Catalog product. Stock records are retained
rather than deleted while historical reservations or adjustments refer to them.

Adjustments require a unique RequestId, a non-zero signed Delta, and a Reason.
Replaying the same request ID and content does not apply the adjustment twice.
Reusing that ID with different content returns 409. Negative adjustments cannot
reduce OnHand below Reserved. The history endpoint lists explicit manual adjustments;
reservation operations are recorded separately in reservation records.

Reservations use OrderId as their idempotency key. A reservation holds 1–100 distinct
products with quantities of 1–100000. Reserving reduces Available but leaves OnHand
unchanged. All stock lines are locked in a consistent order and changed in a single
database transaction, so a partially fulfilled reservation is not saved.

- Reserved -> Committed consumes OnHand and clears the reserved units.
- Reserved -> Released clears the reserved units without consuming OnHand.
- Committed -> Released restores consumed OnHand for a cancellation before shipment.
- Repeated commit/release requests do not apply stock changes twice.
- Released reservations cannot be reserved or committed again.
- Release of an unknown OrderId records a released tombstone. This prevents a
  delayed reserve request from placing a hold after Order has cancelled it.

Reservation and adjustment operations use transaction-scoped PostgreSQL advisory
locks for their request IDs and row locks for stock balances. The database also
enforces non-negative stock and OnHand >= Reserved. Reservation Items and stock
adjustments refer only to local stock records; there are no cross-service foreign keys.

Standalone reservations hold stock until committed or released. Orders manage the
expiration of their own unconfirmed holds. Other callers must release abandoned
reservations themselves.

### Inventory routes

| Method | Route | Purpose |
| --- | --- | --- |
| POST | /api/inventory/stock | Initialize stock; 201 |
| GET | /api/inventory/stock | Paginated balances |
| GET | /api/inventory/stock/{productId} | Balance or 404 |
| POST | /api/inventory/stock/{productId}/adjustments | Apply idempotent adjustment |
| GET | /api/inventory/stock/{productId}/adjustments | Paginated adjustment history |
| POST | /api/inventory/reservations | Reserve all order items |
| GET | /api/inventory/reservations | Paginated reservations |
| GET | /api/inventory/reservations/{orderId} | Reservation or 404 |
| POST | /api/inventory/reservations/{orderId}/commit | Consume reserved stock |
| POST | /api/inventory/reservations/{orderId}/release | Release or restore stock |

List routes accept pageNumber and pageSize (default 1 and 20, maximum size 100).
Stock listing accepts maxAvailable for low-stock filtering. Reservation listing
accepts status=Reserved, Committed, or Released. Statuses serialize as strings.

Invalid input returns 400, missing records return 404, insufficient stock or
conflicting idempotency requests return 409, and unavailable dependencies return 503.

## Order behavior

Create requests supply RequestId, CustomerId, ShippingAddress, and product IDs with
quantities. RequestId becomes the Order ID. Keep it for retries. The service
fingerprints the normalized address, customer, and sorted line requests. Reusing
the ID for different content returns 409.

Order fetches product names, currencies, and prices directly from Catalog and
rejects discontinued products or mixed currencies. Clients do not set prices,
totals, or status. Product names and unit prices are saved as immutable snapshots;
later Catalog changes do not alter an existing order. Totals are line price times
quantity, excluding tax, discounts, and shipping charges.

Creation saves PendingInventory before contacting Inventory. Successful reservation
moves the order to Reserved and returns 201. When Inventory is unavailable or has
insufficient stock, the saved order remains PendingInventory and creation returns
202 with its ID and Location. The same request may be retried after stock is added.

The lifecycle is:

PendingInventory -> Reserved -> Confirming -> Confirmed -> Shipped -> Delivered

PendingInventory, Reserved, and Confirmed may enter Cancelling -> Cancelled.
Shipped and Delivered orders cannot be cancelled through this workflow. Confirmation
commits inventory and now requires a recorded successful payment. The development
Payment service records simulated payments; no real customer funds are charged.

Confirming and Cancelling are saved before the associated Inventory call.
A timeout or crash leaves a durable intermediate state. Retrying resumes the
idempotent stock operation and completes the transition. If confirmation is
uncertain, finish its recovery before starting cancellation. Cancellation of a
confirmed order restores committed stock and completes a development refund before
reaching Cancelled; a reserved unpaid order merely releases its hold.

Shipping requires a carrier and tracking number. Repeating ship with the same
tracking information, delivery, confirmation, and cancellation does not repeat the
inventory effects. Closed orders and immutable lines are retained for history.
Mutation requests for one order serialize through a PostgreSQL advisory lock;
UpdatedAt additionally protects conflicting writes.

### Recovery and hold expiration

OrderRecoveryWorker runs a bounded recovery pass using a fresh dependency scope
per order. It retries PendingInventory, Confirming, and Cancelling, and cancels
unconfirmed PendingInventory/Reserved orders older than the configured hold period.
Cancellation releases inventory through the same idempotent workflow as the API.

Recovery attempt timestamps are persisted, so repeatedly failing orders do not
permanently occupy the oldest positions in a recovery batch. Multiple Order
instances may run the worker; order-level locks and idempotent Inventory calls
serialize each operation.

Configure in appsettings or environment variables:

- OrderRecovery:Enabled (default true).
- OrderRecovery:IntervalSeconds (default 30).
- OrderRecovery:HoldMinutes (default 30, measured from order creation).
- OrderRecovery:BatchSize (default 50).

Setting Enabled=false disables automatic recovery/expiration while keeping
POST /api/orders/{id}/retry available. Dependency failures are logged and retried
in later passes. This is a database-backed recovery worker, without a broker or outbox.

### Order routes

| Method | Route | Purpose |
| --- | --- | --- |
| POST | /api/orders | Create/idempotently replay; 201 or pending 202 |
| GET | /api/orders | Paginated orders |
| GET | /api/orders/{id} | Order and snapshots or 404 |
| POST | /api/orders/{id}/confirm | Commit stock and confirm |
| POST | /api/orders/{id}/cancel | Cancel before shipment and release/restore stock |
| POST | /api/orders/{id}/retry | Resume unfinished stock operation |
| POST | /api/orders/{id}/ship | Record carrier/tracking and ship |
| POST | /api/orders/{id}/deliver | Mark shipped order delivered |

List accepts pageNumber, pageSize, customerId, and status. Statuses serialize as
strings. Invalid data returns 400, missing orders 404, incompatible transitions or
idempotency conflicts 409, and unavailable dependencies 503. A failed create call
after local persistence can be inspected using RequestId as the Order ID.

## Running locally

Catalog uses port 5171, Inventory 5172, and Order 5173. Each exposes /swagger and
/openapi/v1.json in Development, /health/live, and /health/ready. Readiness requires
database access and all migrations applied. The APIs do not apply migrations on
normal startup.

Provide a PostgreSQL instance with catalog_db, inventory_db, and order_db. Override
development example connections with .NET User Secrets or environment variables:

- ConnectionStrings__InventoryDatabase
- ConnectionStrings__OrderDatabase
- Services__Catalog (defaults to http://localhost:5171/)
- Services__Inventory (Order only; defaults to http://localhost:5172/)

Build and apply migrations explicitly:

```powershell
dotnet build ECommerce.slnx '-p:Platform=Any CPU'
dotnet run --project src/ECommerce.Catalog.Api --no-build --no-launch-profile '-p:Platform=AnyCPU' -- --migrate
dotnet run --project src/ECommerce.Inventory.Api --no-build --no-launch-profile '-p:Platform=AnyCPU' -- --migrate
dotnet run --project src/ECommerce.Order.Api --no-build --no-launch-profile '-p:Platform=AnyCPU' -- --migrate
```

Start each API in its own terminal:

```powershell
dotnet run --project src/ECommerce.Catalog.Api --launch-profile http '-p:Platform=AnyCPU'
dotnet run --project src/ECommerce.Inventory.Api --launch-profile http '-p:Platform=AnyCPU'
dotnet run --project src/ECommerce.Order.Api --launch-profile http '-p:Platform=AnyCPU'
```

Use src/ECommerce.Inventory.Api/ECommerce.Inventory.Api.http and
src/ECommerce.Order.Api/ECommerce.Order.Api.http for request examples. First create a
category/product in Catalog, initialize its Inventory stock, then place an order.

## Docker

The root Compose file includes PostgreSQL, Catalog, Inventory, and Order. No message
broker is included. A new PostgreSQL volume initializes inventory_db and order_db
from docker/postgres/init-databases.sql, alongside POSTGRES_DB=catalog_db.

```powershell
docker compose up -d postgres
docker compose build catalog inventory order
docker compose run --rm --no-deps catalog --migrate
docker compose run --rm --no-deps inventory --migrate
docker compose run --rm --no-deps order --migrate
docker compose up -d catalog inventory order
```

For an existing PostgreSQL volume, initialization scripts do not run again. Create
the two missing databases without deleting the existing Catalog data:

```powershell
docker compose exec postgres psql -U ecommerce -d postgres -c 'CREATE DATABASE inventory_db;'
docker compose exec postgres psql -U ecommerce -d postgres -c 'CREATE DATABASE order_db;'
```

Run each creation command only if that database is absent. Existing credentials
remain those used when the volume was initialized.

## Integration boundaries

Identity now supplies customer IDs from validated tokens. Customers access their
own orders, administrators manage stock/fulfillment, and service credentials protect
reservation and internal payment operations. See the integration documentation.

Simulated payments and refunds are integrated with Order. Real payment capture,
refunds, carrier integrations, and returns processing remain separate integrations.
Confirmed is backed by a successful development receipt, not real payment settlement.
Message queue logic and a transactional outbox remain deferred.

See [Automated tests](testing.md) for coverage, current results, and database setup.
