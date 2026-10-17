DO $$ BEGIN
    IF EXISTS (
        SELECT FROM products p LEFT JOIN seed_products s ON s.id = p."Id"
        WHERE s.id IS NULL OR p."Name" <> s.name OR p.price <> s.price OR p.currency <> 'USD'
           OR p."CategoryId" <> s.category_id OR NOT p."IsActive"
    ) THEN RAISE EXCEPTION 'Catalog fixture mismatch'; END IF;
END $$;
