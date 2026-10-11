# ECommerce

## Catalog service

Catalog now implements category CRUD and product creation, retrieval, updating,
price changes, discontinuation, pagination, and filtering using four layers:

- Domain owns the Product and Category aggregates, Money, invariants, and domain events.
- Application owns commands, queries, handlers, DTOs, and persistence contracts.
- Infrastructure owns EF Core, PostgreSQL mappings, repositories, queries, and migrations.
- API owns HTTP routing, dependency injection, ProblemDetails, OpenAPI, and health endpoints.

Product and Category are independent aggregates; products reference categories by ID.
The Domain has no external package dependencies. Inventory quantities remain outside Catalog.

## Behavior

Category responses include Id, Name, Description, CreatedAt, and UpdatedAt.
Names and descriptions are trimmed. Blank descriptions become null. Category names
are unique and case-sensitive. Names are limited to 150 characters for categories
and 200 for products; descriptions are limited to 1,000 and 2,000 respectively.

Money allows non-negative amounts within decimal(18, 2), with at most two decimal
places. Currency codes must contain three ASCII letters and are normalized to
uppercase; the format is validated without an ISO currency lookup. Currency cannot
be changed on an existing product. Price must be explicitly supplied in create,
update, and price-change request bodies.

DELETE for products discontinues them rather than removing rows. Repeated
discontinuation returns 204. Discontinued products remain accessible by ID and
retain historical references. Categories containing any products, including
discontinued products, cannot be deleted.

Listings default to active products. Search is a case-insensitive literal substring
of the name, so percent and underscore are not wildcards. Results are ordered by
name, then ID. Page numbers start at 1; page size defaults to 20 and must be 1–100.
The response contains items, totalCount, pageNumber, and pageSize. TotalCount is the
count after filtering and before pagination.

UpdatedAt is an optimistic concurrency token for both aggregates. Competing
requests that load the same original version and save changes return a 409 conflict
for the losing request. This protects concurrent saves within request lifetimes;
ETag/If-Match checks for edits submitted from an older client view are not implemented.

Domain events stay within the aggregate in memory. No broker, RabbitMQ, event
publisher, transactional outbox, or worker is required for the current standalone
Catalog workflows. Integration events can be added when Inventory or Order need them.
Authentication and administrator permissions will need integration with Identity
before exposing write endpoints beyond the local development environment.

## Endpoints

| Method | Route | Result |
| --- | --- | --- |
| POST | /api/categories | Create; 201 and Location |
| GET | /api/categories | Sorted list |
| GET | /api/categories/{id} | Details or 404 |
| PUT | /api/categories/{id} | Replace name/description; 204 |
| DELETE | /api/categories/{id} | Delete unused category; 204 |
| POST | /api/products | Create; 201 and Location |
| GET | /api/products | Paginated list |
| GET | /api/products/{id} | Details, including discontinued products, or 404 |
| PUT | /api/products/{id} | Replace name/description/price/currency/categoryId; 204 |
| PATCH | /api/products/{id}/price | Change price using price/currency; 204 |
| DELETE | /api/products/{id} | Discontinue; 204 |
| GET | /health/live | Process liveness |
| GET | /health/ready | Database reachable and all migrations applied |

Product list query parameters:

- pageNumber and pageSize for pagination.
- categoryId to limit results to a category.
- search for a literal, case-insensitive name substring.
- isActive=true or isActive=false for an explicit lifecycle filter.
- includeInactive=true to include both states when isActive is omitted.

Invalid input or a missing product category returns 400 ProblemDetails. Duplicate
category names, category deletion with products, and concurrent saves return 409
ProblemDetails. Missing mutation targets return 404. Request examples are in
ECommerce.Catalog.Api/ECommerce.Catalog.Api.http.

## Run locally

Use a PostgreSQL server with a catalog_db database. The appsettings connection is a
local development example. Override it using .NET User Secrets or
ConnectionStrings__CatalogDatabase for your environment.

From the solution root:

```powershell
dotnet build ECommerce.slnx '-p:Platform=Any CPU'
dotnet run --project ECommerce.Catalog.Api --no-build --no-launch-profile '-p:Platform=AnyCPU' -- --migrate
dotnet run --project ECommerce.Catalog.Api --launch-profile http '-p:Platform=AnyCPU'
```

The --migrate mode applies migrations and exits; normal API startup does not modify
the schema. In Development, Swagger UI is at http://localhost:5171/swagger and the
OpenAPI document is at /openapi/v1.json. Readiness returns 503 until PostgreSQL is
available and migrations are applied.

For EF migration development, install the CLI once:

```powershell
dotnet tool install --tool-path .tools dotnet-ef --version 10.0.12
$env:Platform = 'AnyCPU'
.\.tools\dotnet-ef.exe migrations add MigrationName --project ECommerce.Catalog.Infrastructure --startup-project ECommerce.Catalog.Api --output-dir Persistence/Migrations
```

AddCatalogConcurrency records the concurrency mapping in the model snapshot and
has no database DDL because the existing UpdatedAt columns are reused.

## Docker development

The root Compose file starts PostgreSQL and Catalog, without a message broker.
The Catalog Dockerfile restores all four projects. Local ports are bound to loopback.

```powershell
docker compose up -d postgres
docker compose build catalog
docker compose run --rm catalog --migrate
docker compose up -d catalog
```

The API is then at http://localhost:5171. PostgreSQL data persists in the
postgres_data volume. Existing PostgreSQL volumes retain their original database
and credentials; Compose initialization settings apply only to a new volume.

## Verification

```powershell
dotnet build ECommerce.slnx '-p:Platform=Any CPU'
dotnet run --project tests/ECommerce.Catalog.Checks --no-build --no-restore '-p:Platform=AnyCPU'
```

Checks cover domain invariants, event behavior, lifecycle changes, atomic domain
updates, PostgreSQL schema/query generation, migration consistency, provider-error
translation, and the real HTTP pipeline using repository test doubles. The HTTP
checks cover both aggregates, paging/filtering, validation, missing IDs, conflicts,
price changes, discontinuation, OpenAPI, Swagger UI, and liveness.

Optional live PostgreSQL checks use CATALOG_TEST_CONNECTION_STRING. Point it at a
development/test database whose user can create schemas. The checks create a
uniquely named schema and apply migrations. They verify category CRUD (including
duplicate renames, UTC timestamps, description clearing, and concurrent updates),
product workflows, and product concurrency, then remove only that schema afterward.
Without this variable, live
checks explicitly report that they were skipped.

```powershell
$env:CATALOG_TEST_CONNECTION_STRING = 'Host=localhost;Port=5432;Database=catalog_db;Username=ecommerce;Password=ecommerce'
dotnet run --project tests/ECommerce.Catalog.Checks --no-build --no-restore '-p:Platform=AnyCPU'
```
