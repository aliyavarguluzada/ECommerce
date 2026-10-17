DO $$ BEGIN
    IF EXISTS (
        SELECT FROM carts c LEFT JOIN generate_series(1, 20000) g(i) ON c."CustomerId" = pg_temp.seed_id('user', i)
        WHERE i IS NULL OR c."PendingCheckoutId" IS NOT NULL
    ) OR EXISTS (
        SELECT FROM checkout_attempts c LEFT JOIN seed_orders s ON s.id = c."RequestId"
        LEFT JOIN seed_addresses a ON a.order_id = c."RequestId"
        WHERE s.id IS NULL OR c."CustomerId" <> s.customer_id OR c."Status" <> 'Completed'
           OR c."AddressJson" <> a.json OR c."AddressHash" <> upper(encode(sha256(convert_to(a.json, 'UTF8')), 'hex'))
    ) OR EXISTS (
        SELECT FROM checkout_items c LEFT JOIN seed_lines s ON s.order_id = c."RequestId" AND s.product_id = c."ProductId"
        WHERE s.order_id IS NULL OR c."Quantity" <> s.quantity
    ) OR EXISTS (
        SELECT FROM cart_items c LEFT JOIN seed_products s ON s.id = c."ProductId"
        WHERE s.id IS NULL OR c."Quantity" NOT BETWEEN 1 AND 3
    ) THEN RAISE EXCEPTION 'Cart/customer/checkout/product mismatch or pending checkout'; END IF;
END $$;
