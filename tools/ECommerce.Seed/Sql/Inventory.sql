INSERT INTO stock_items ("ProductId", "OnHand", "Reserved", "CreatedAt", "UpdatedAt")
SELECT p.id, 10000 - coalesce(sold.quantity, 0)::integer, 0, timestamptz '2025-01-01', timestamptz '2026-09-01'
FROM seed_products p LEFT JOIN (
    SELECT l.product_id, sum(l.quantity) AS quantity FROM seed_lines l JOIN seed_orders o ON o.id = l.order_id
    WHERE o.status <> 'Cancelled' GROUP BY l.product_id
) sold ON sold.product_id = p.id;

INSERT INTO stock_adjustments ("RequestId", "ProductId", "Delta", "Reason", "CreatedAt")
SELECT pg_temp.seed_id('stock-adjustment', n), id, 10000, 'Synthetic initial stock', timestamptz '2025-01-01'
FROM seed_products;

INSERT INTO reservations ("OrderId", "Status", "CreatedAt", "UpdatedAt")
SELECT id, CASE WHEN status = 'Cancelled' THEN 'Released' ELSE 'Committed' END,
       created_at, created_at + interval '2 days' FROM seed_orders;

INSERT INTO reservation_items ("OrderId", "ProductId", "Quantity")
SELECT order_id, product_id, quantity FROM seed_lines;
