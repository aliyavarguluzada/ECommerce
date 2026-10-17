DO $$ BEGIN
    IF EXISTS (
        SELECT FROM reservations r LEFT JOIN seed_orders s ON s.id = r."OrderId"
        WHERE s.id IS NULL OR r."Status" <> CASE WHEN s.status = 'Cancelled' THEN 'Released' ELSE 'Committed' END
    ) OR EXISTS (
        SELECT FROM reservation_items r LEFT JOIN seed_lines s ON s.order_id = r."OrderId" AND s.product_id = r."ProductId"
        WHERE s.order_id IS NULL OR r."Quantity" <> s.quantity
    ) OR EXISTS (
        SELECT FROM stock_items p LEFT JOIN seed_products s ON s.id = p."ProductId"
        LEFT JOIN (
            SELECT l.product_id, sum(l.quantity) AS sold FROM seed_lines l JOIN seed_orders o ON o.id = l.order_id
            WHERE o.status <> 'Cancelled' GROUP BY l.product_id
        ) sold ON sold.product_id = p."ProductId"
        WHERE s.id IS NULL OR p."Reserved" <> 0 OR p."OnHand" <> 10000 - coalesce(sold.sold, 0)
    ) THEN RAISE EXCEPTION 'Inventory/order reservation or stock balance mismatch'; END IF;
END $$;
