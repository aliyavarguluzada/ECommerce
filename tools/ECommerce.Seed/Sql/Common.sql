-- Versioned, deterministic identifiers are shared across all six databases.
CREATE OR REPLACE FUNCTION pg_temp.seed_id(kind text, number integer)
RETURNS uuid LANGUAGE sql IMMUTABLE AS $$
    SELECT md5('ecommerce-seed-v1:' || kind || ':' || number)::uuid
$$;

CREATE TEMP TABLE seed_products AS
SELECT i AS n, pg_temp.seed_id('product', i) AS id,
       pg_temp.seed_id('category', (i - 1) / 20 + 1) AS category_id,
       (ARRAY['Headphones','Keyboard','Lamp','Backpack','Watch','Chair','Notebook','Bottle','Speaker','Jacket'])[(i - 1) % 10 + 1]
           || ' Model ' || lpad(i::text, 4, '0') AS name,
       (5 + (i * 137 % 49995) / 100.0)::numeric(18,2) AS price
FROM generate_series(1, 1000) AS g(i);

CREATE TEMP TABLE seed_orders AS
SELECT i AS n, pg_temp.seed_id('order', i) AS id,
       pg_temp.seed_id('user', i) AS customer_id,
       CASE WHEN i % 10 = 0 THEN 'Cancelled'
            WHEN i % 10 = 1 THEN 'Confirmed'
            WHEN i % 10 IN (2,3) THEN 'Shipped' ELSE 'Delivered' END AS status,
       CASE WHEN i % 10 <> 0 OR i % 20 = 0 THEN pg_temp.seed_id('payment', i) END AS payment_id,
       i % 20 = 0 AS refunded,
       'Seed Customer ' || i AS recipient,
       (i % 999 + 1) || ' Example Street' AS line1,
       (ARRAY['Baku','Ganja','Sumqayit','Shaki','Lankaran'])[i % 5 + 1] AS city,
       timestamptz '2026-01-01 08:00:00+00' + (i % 240) * interval '1 day'
           + (i % 3600) * interval '1 second' AS created_at
FROM generate_series(1, 100000) AS g(i);

CREATE UNIQUE INDEX ON seed_orders(id);
CREATE UNIQUE INDEX ON seed_orders(payment_id);

CREATE TEMP TABLE seed_lines AS
SELECT o.n AS order_n, o.id AS order_id, p.id AS product_id, p.name AS product_name,
       ((o.n + l.i) % 3 + 1)::integer AS quantity, p.price AS unit_price
FROM seed_orders o
CROSS JOIN generate_series(1, 3) l(i)
JOIN seed_products p ON p.n = ((o.n * 17 + l.i * 29) % 1000) + 1;

CREATE INDEX ON seed_lines(order_id);

CREATE TEMP TABLE seed_totals AS
SELECT order_id, sum(quantity * unit_price)::numeric(18,2) AS total
FROM seed_lines GROUP BY order_id;

-- Match System.Text.Json's declaration order and compact representation for replay hashes.
CREATE TEMP TABLE seed_addresses AS
SELECT n, id AS order_id,
       '{"Recipient":"' || recipient || '","Line1":"' || line1
       || '","Line2":null,"City":"' || city
       || '","Region":null,"PostalCode":"AZ1000","CountryCode":"AZ"}' AS json
FROM seed_orders;

CREATE TEMP TABLE seed_fingerprints AS
SELECT o.id AS order_id,
       upper(encode(sha256(convert_to(
           '{"CustomerId":"' || o.customer_id || '","Address":' || a.json || ',"Items":['
           || string_agg('{"ProductId":"' || l.product_id || '","Quantity":' || l.quantity || '}', ',' ORDER BY l.product_id)
           || ']}', 'UTF8')), 'hex')) AS fingerprint
FROM seed_orders o JOIN seed_addresses a ON a.order_id = o.id
JOIN seed_lines l ON l.order_id = o.id
GROUP BY o.id, o.customer_id, a.json;

CREATE UNIQUE INDEX ON seed_products(id);
CREATE UNIQUE INDEX ON seed_totals(order_id);
CREATE UNIQUE INDEX ON seed_addresses(order_id);
CREATE UNIQUE INDEX ON seed_fingerprints(order_id);
CREATE UNIQUE INDEX ON seed_lines(order_id, product_id);

ANALYZE seed_products;
ANALYZE seed_orders;
ANALYZE seed_lines;
ANALYZE seed_totals;
ANALYZE seed_addresses;
ANALYZE seed_fingerprints;
