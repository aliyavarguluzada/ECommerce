INSERT INTO categories ("Id", "Name", "Description", "CreatedAt", "UpdatedAt")
SELECT pg_temp.seed_id('category', i), 'Category ' || lpad(i::text, 2, '0'),
       'Synthetic seed category ' || i, timestamptz '2025-01-01', timestamptz '2025-01-01'
FROM generate_series(1, 50) AS g(i);

INSERT INTO products ("Id", "Name", "Description", "CategoryId", price, currency, "IsActive", "CreatedAt", "UpdatedAt")
SELECT id, name, 'Synthetic demonstration product; no real inventory or price.', category_id,
       price, 'USD', true, timestamptz '2025-01-01', timestamptz '2025-01-01'
FROM seed_products;
