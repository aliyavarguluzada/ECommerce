DO $$ BEGIN
    IF EXISTS (
        SELECT FROM payments p LEFT JOIN seed_orders s ON s.payment_id = p."Id"
        LEFT JOIN seed_totals t ON t.order_id = p."OrderId"
        WHERE s.id IS NULL OR p."OrderId" <> s.id OR p."CustomerId" <> s.customer_id
           OR p."Amount" <> t.total OR p."Currency" <> 'USD' OR NOT p."OrderNotified"
           OR p."Status" <> CASE WHEN s.refunded THEN 'Refunded' ELSE 'Succeeded' END
    ) THEN RAISE EXCEPTION 'Payment/order/customer/amount mismatch or pending notification'; END IF;
END $$;
