-- =============================================================================
-- ShopApp — Supabase / PostgreSQL schema
-- =============================================================================
-- Run this once against your Supabase project to create the tables.
--
-- How to apply:
--   Option A — Supabase SQL Editor (easiest):
--     Dashboard → SQL Editor → New query → paste this file → Run
--
--   Option B — psql:
--     psql "$DATABASE_URL" -f supabase/migrations/001_initial_schema.sql
--
--   Option C — Supabase CLI:
--     supabase db push
-- =============================================================================

-- Enable UUID extension (optional — used if you ever switch PKs to uuid)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ── customers ─────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS customers (
    customer_id      SERIAL PRIMARY KEY,
    full_name        TEXT        NOT NULL,
    email            TEXT,
    gender           TEXT,
    birthdate        DATE,
    created_at       TIMESTAMP,
    city             TEXT,
    state            TEXT,
    zip_code         TEXT,
    customer_segment TEXT,
    loyalty_tier     TEXT,
    is_active        SMALLINT    NOT NULL DEFAULT 1
);

-- ── products ──────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS products (
    product_id   SERIAL PRIMARY KEY,
    sku          TEXT,
    product_name TEXT           NOT NULL,
    category     TEXT,
    price        NUMERIC(10,2)  NOT NULL,
    cost         NUMERIC(10,2)  NOT NULL,
    is_active    SMALLINT       NOT NULL DEFAULT 1
);

-- ── orders ────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS orders (
    order_id        SERIAL PRIMARY KEY,
    customer_id     INTEGER        NOT NULL REFERENCES customers(customer_id),
    order_datetime  TIMESTAMP      NOT NULL DEFAULT NOW(),
    billing_zip     TEXT,
    shipping_zip    TEXT,
    shipping_state  TEXT,
    payment_method  TEXT,
    device_type     TEXT,
    ip_country      TEXT,
    promo_used      SMALLINT       NOT NULL DEFAULT 0,
    promo_code      TEXT,
    order_subtotal  NUMERIC(10,2)  NOT NULL DEFAULT 0,
    shipping_fee    NUMERIC(10,2)  NOT NULL DEFAULT 0,
    tax_amount      NUMERIC(10,2)  NOT NULL DEFAULT 0,
    order_total     NUMERIC(10,2)  NOT NULL DEFAULT 0,
    risk_score      NUMERIC(5,2)   NOT NULL DEFAULT 0,
    is_fraud        SMALLINT       NOT NULL DEFAULT 0
);

-- ── order_items ───────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS order_items (
    order_item_id  SERIAL PRIMARY KEY,
    order_id       INTEGER       NOT NULL REFERENCES orders(order_id),
    product_id     INTEGER       NOT NULL REFERENCES products(product_id),
    quantity       INTEGER       NOT NULL DEFAULT 1,
    unit_price     NUMERIC(10,2) NOT NULL,
    line_total     NUMERIC(10,2) NOT NULL
);

-- ── shipments ─────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS shipments (
    shipment_id     SERIAL PRIMARY KEY,
    order_id        INTEGER   NOT NULL REFERENCES orders(order_id),
    ship_datetime   TIMESTAMP,
    carrier         TEXT,
    shipping_method TEXT,
    distance_band   TEXT,
    promised_days   INTEGER,
    actual_days     INTEGER,
    -- Ground-truth flag: 1 = late, 0 = on time, NULL = not yet delivered
    late_delivery   SMALLINT
);

-- ── product_reviews ───────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS product_reviews (
    review_id       SERIAL PRIMARY KEY,
    customer_id     INTEGER NOT NULL REFERENCES customers(customer_id),
    product_id      INTEGER NOT NULL REFERENCES products(product_id),
    rating          SMALLINT,
    review_datetime TIMESTAMP,
    review_text     TEXT
);

-- ── Indexes (mirror the SQLite indexes for query performance) ─────────────────
CREATE INDEX IF NOT EXISTS idx_orders_customer    ON orders(customer_id);
CREATE INDEX IF NOT EXISTS idx_orders_datetime    ON orders(order_datetime);
CREATE INDEX IF NOT EXISTS idx_items_order        ON order_items(order_id);
CREATE INDEX IF NOT EXISTS idx_items_product      ON order_items(product_id);
CREATE INDEX IF NOT EXISTS idx_shipments_late     ON shipments(late_delivery);
CREATE INDEX IF NOT EXISTS idx_reviews_product    ON product_reviews(product_id);
CREATE INDEX IF NOT EXISTS idx_reviews_customer   ON product_reviews(customer_id);

-- ── Row Level Security (optional but recommended) ─────────────────────────────
-- The app connects as the postgres role (service role), so RLS is not strictly
-- required. Enable and configure it if you want per-user data isolation later.
-- ALTER TABLE customers ENABLE ROW LEVEL SECURITY;
-- ALTER TABLE orders    ENABLE ROW LEVEL SECURITY;
