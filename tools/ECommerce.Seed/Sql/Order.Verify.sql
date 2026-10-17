DO $$ BEGIN
    IF EXISTS (
        SELECT FROM orders o LEFT JOIN seed_orders s ON s.id = o."Id"
        LEFT JOIN seed_totals t ON t.order_id = o."Id"
        LEFT JOIN seed_fingerprints f ON f.order_id = o."Id"
        WHERE s.id IS NULL OR o."CustomerId" <> s.customer_id OR o."Total" <> t.total
           OR o."Currency" <> 'USD' OR o."Status" <> s.status
           OR o."PaymentId" IS DISTINCT FROM s.payment_id OR o."PaymentRefunded" <> s.refunded
           OR o."RequestFingerprint" <> f.fingerprint
           OR o."ShippingAddress_Recipient" <> s.recipient OR o."ShippingAddress_Line1" <> s.line1
           OR o."ShippingAddress_City" <> s.city
           OR (o."Status" IN ('Shipped','Delivered') AND (o."Carrier" IS NULL OR o."TrackingNumber" IS NULL))
    ) OR EXISTS (
        SELECT FROM order_items l LEFT JOIN seed_lines s ON s.order_id = l."OrderId" AND s.product_id = l."ProductId"
        WHERE s.order_id IS NULL OR l."Quantity" <> s.quantity OR l."UnitPrice" <> s.unit_price OR l."ProductName" <> s.product_name
    ) THEN RAISE EXCEPTION 'Order/customer/product/payment/total or replay fingerprint mismatch'; END IF;
END $$;
