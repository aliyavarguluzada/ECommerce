DO $$ BEGIN
    IF EXISTS (
        SELECT FROM "AspNetUsers" u
        LEFT JOIN generate_series(1, 100000) g(i) ON u."Id" = pg_temp.seed_id('user', i)
        WHERE i IS NULL OR u."Email" <> 'user' || i || '@example.test'
           OR u."NormalizedEmail" <> upper(u."Email") OR NOT u."IsActive"
    ) OR (SELECT count(DISTINCT "PasswordHash") FROM "AspNetUsers" WHERE "PasswordHash" IS NOT NULL) <> 10
      OR EXISTS (SELECT FROM refresh_sessions WHERE "RevokedAt" IS NULL OR "ExpiresAt" > now())
    THEN RAISE EXCEPTION 'Identity fixture mismatch or active synthetic refresh sessions'; END IF;
END $$;
