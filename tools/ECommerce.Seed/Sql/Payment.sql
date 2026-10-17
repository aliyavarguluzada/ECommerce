INSERT INTO payments (
    "Id", "OrderId", "CustomerId", "Amount", "Currency", "MethodToken", "Status", "ProviderReference",
    "FailureReason", "OrderNotified", "CreatedAt", "UpdatedAt", "LastRecoveryAttemptAt")
SELECT o.payment_id, o.id, o.customer_id, t.total, 'USD', 'dev-approved',
       CASE WHEN o.refunded THEN 'Refunded' ELSE 'Succeeded' END, 'seed-charge-' || o.n,
       NULL, true, o.created_at + interval '1 minute', o.created_at + interval '2 days', NULL
FROM seed_orders o JOIN seed_totals t ON t.order_id = o.id WHERE o.payment_id IS NOT NULL;
