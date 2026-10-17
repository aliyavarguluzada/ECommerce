INSERT INTO carts ("CustomerId", "PendingCheckoutId", "UpdatedAt")
SELECT pg_temp.seed_id('user', i), NULL, timestamptz '2026-09-01' FROM generate_series(1, 20000) AS g(i);

INSERT INTO cart_items ("CustomerId", "ProductId", "Quantity")
SELECT pg_temp.seed_id('user', i), pg_temp.seed_id('product', ((i * 13 + l * 31) % 1000) + 1), (i + l) % 3 + 1
FROM generate_series(1, 20000) AS g(i) CROSS JOIN generate_series(1, 3) AS lines(l);

INSERT INTO checkout_attempts ("RequestId", "CustomerId", "AddressJson", "AddressHash", "Status", "CreatedAt")
SELECT o.id, o.customer_id, a.json, upper(encode(sha256(convert_to(a.json, 'UTF8')), 'hex')), 'Completed', o.created_at
FROM seed_orders o JOIN seed_addresses a ON a.order_id = o.id;

INSERT INTO checkout_items ("RequestId", "ProductId", "Quantity")
SELECT order_id, product_id, quantity FROM seed_lines;
