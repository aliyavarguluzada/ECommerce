INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
VALUES (pg_temp.seed_id('role', 1), 'Customer', 'CUSTOMER', 'seed-v1-customer'),
       (pg_temp.seed_id('role', 2), 'Admin', 'ADMIN', 'seed-v1-admin');

INSERT INTO "AspNetUsers" (
    "Id", "DisplayName", "IsActive", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
    "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumberConfirmed",
    "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
SELECT pg_temp.seed_id('user', i), 'Seed Customer ' || i, true,
       'user' || i || '@example.test', upper('user' || i || '@example.test'),
       'user' || i || '@example.test', upper('user' || i || '@example.test'), true,
       NULL, md5('seed-security:' || i), md5('seed-concurrency:' || i), false, false, true, 0
FROM generate_series(1, 100000) AS g(i);

INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT "Id", pg_temp.seed_id('role', 1) FROM "AspNetUsers";

INSERT INTO "AspNetUserRoles" ("UserId", "RoleId") VALUES (pg_temp.seed_id('user', 1), pg_temp.seed_id('role', 2));

INSERT INTO "AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT "Id", 'seed_dataset', 'ecommerce-seed-v1' FROM "AspNetRoles";

INSERT INTO "AspNetUserClaims" ("UserId", "ClaimType", "ClaimValue")
SELECT "Id", 'seed_dataset', 'ecommerce-seed-v1' FROM "AspNetUsers";

-- Inert examples populate framework tables without usable external credentials or tokens.
INSERT INTO "AspNetUserLogins" ("LoginProvider", "ProviderKey", "ProviderDisplayName", "UserId")
SELECT 'SeedOnly', 'seed-external-' || i, 'Synthetic provider (not configured)', pg_temp.seed_id('user', i)
FROM generate_series(11, 1010) AS g(i);

INSERT INTO "AspNetUserTokens" ("UserId", "LoginProvider", "Name", "Value")
SELECT pg_temp.seed_id('user', i), 'SeedOnly', 'DatasetMarker', 'not-an-authentication-token'
FROM generate_series(1, 1000) AS g(i);

INSERT INTO refresh_sessions ("TokenHash", "UserId", "FamilyId", "ExpiresAt", "RevokedAt", "ReplacementHash")
SELECT upper(encode(sha256(convert_to('expired-seed-session:' || i, 'UTF8')), 'hex')),
       pg_temp.seed_id('user', i), pg_temp.seed_id('family', i),
       timestamptz '2025-01-02', timestamptz '2025-01-01', NULL
FROM generate_series(1, 1000) AS g(i);
