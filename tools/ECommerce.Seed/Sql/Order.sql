INSERT INTO orders (
    "Id", "CustomerId", "RequestFingerprint", "Status", "Currency",
    "ShippingAddress_Recipient", "ShippingAddress_Line1", "ShippingAddress_Line2", "ShippingAddress_City",
    "ShippingAddress_Region", "ShippingAddress_PostalCode", "ShippingAddress_CountryCode",
    "Total", "CancellationReason", "Carrier", "TrackingNumber", "CreatedAt", "UpdatedAt",
    "LastRecoveryAttemptAt", "PaymentId", "PaymentRefunded")
SELECT o.id, o.customer_id, f.fingerprint, o.status, 'USD', o.recipient, o.line1, NULL, o.city,
       NULL, 'AZ1000', 'AZ', t.total,
       CASE WHEN o.status = 'Cancelled' THEN 'Synthetic customer cancellation' END,
       CASE WHEN o.status IN ('Shipped','Delivered') THEN 'Seed Carrier' END,
       CASE WHEN o.status IN ('Shipped','Delivered') THEN 'SEED-' || lpad(o.n::text, 10, '0') END,
       o.created_at, o.created_at + interval '2 days', NULL, o.payment_id, o.refunded
FROM seed_orders o JOIN seed_totals t ON t.order_id = o.id JOIN seed_fingerprints f ON f.order_id = o.id;

INSERT INTO order_items ("OrderId", "ProductId", "ProductName", "Quantity", "UnitPrice")
SELECT order_id, product_id, product_name, quantity, unit_price FROM seed_lines;
