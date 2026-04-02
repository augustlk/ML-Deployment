"""
score_orders.py — ML Pipeline Integration Script
=================================================
Loads the trained fraud detection model, scores all live orders from Supabase,
and writes results to the ml_scores table so the web app can display them.

Usage
-----
    python ml/score_orders.py \
        --model fraud_detection_pipeline.pkl \
        --pg "postgresql://postgres:PASSWORD@db.REF.supabase.co:5432/postgres"

Requirements
------------
    pip install psycopg2-binary pandas scikit-learn joblib numpy

Workflow
--------
    1. ML team trains the model in the notebook → saves fraud_detection_pipeline.pkl
    2. Run this script to score all live orders and write to ml_scores in Supabase
    3. The web app reads ml_scores for the warehouse priority queue
    4. Optionally call POST /api/warehouse/score on the live site to refresh the cache
"""

import argparse
import sys
from datetime import datetime, timezone

import joblib
import numpy as np
import pandas as pd
import psycopg2
import psycopg2.extras

# ── Feature constants (must mirror training notebook) ─────────────────────────
CATEGORICAL_COLS = [
    'shipping_state', 'payment_method', 'device_type',
    'gender', 'customer_segment', 'loyalty_tier',
    'carrier', 'shipping_method', 'distance_band'
]

NUMERIC_COLS = [
    'order_subtotal', 'shipping_fee', 'order_total',
    'risk_score', 'promised_days', 'actual_days',
    'order_hour', 'order_dow',
    'ship_delay', 'tax_ratio', 'shipping_ratio'
]

BINARY_COLS = [
    'promo_used', 'is_weekend', 'is_night', 'zip_mismatch',
    'foreign_ip', 'late_delivery', 'customer_is_active'
]

ALL_FEATURES = CATEGORICAL_COLS + NUMERIC_COLS + BINARY_COLS

# ── SQL: fetch all orders with the features the model expects ─────────────────
FETCH_QUERY = """
SELECT
    o.order_id,
    o.customer_id,
    o.order_datetime,
    o.billing_zip,
    o.shipping_zip,
    o.shipping_state,
    o.payment_method,
    o.device_type,
    o.ip_country,
    o.promo_used,
    o.promo_code,
    o.order_subtotal,
    o.shipping_fee,
    o.tax_amount,
    o.order_total,
    o.risk_score,
    c.gender,
    c.customer_segment,
    c.loyalty_tier,
    c.is_active   AS customer_is_active,
    s.carrier,
    s.shipping_method,
    s.distance_band,
    s.promised_days,
    s.actual_days,
    s.late_delivery
FROM orders o
LEFT JOIN customers c ON o.customer_id = c.customer_id
LEFT JOIN shipments s ON o.order_id    = s.order_id
"""

# ── SQL: upsert scores ────────────────────────────────────────────────────────
UPSERT_QUERY = """
INSERT INTO ml_scores (order_id, fraud_probability, risk_level, model_version, scored_at)
VALUES %s
ON CONFLICT (order_id) DO UPDATE SET
    fraud_probability = EXCLUDED.fraud_probability,
    risk_level        = EXCLUDED.risk_level,
    model_version     = EXCLUDED.model_version,
    scored_at         = EXCLUDED.scored_at
"""


def engineer_features(df: pd.DataFrame) -> pd.DataFrame:
    """Apply the same feature engineering used during training."""
    df = df.copy()

    # Datetime features
    df['order_datetime'] = pd.to_datetime(df['order_datetime'], utc=True)
    df['order_hour']     = df['order_datetime'].dt.hour
    df['order_dow']      = df['order_datetime'].dt.dayofweek
    df['is_weekend']     = (df['order_dow'] >= 5).astype(int)
    df['is_night']       = ((df['order_hour'] >= 22) | (df['order_hour'] <= 5)).astype(int)

    # Engineered features
    df['zip_mismatch']   = (df['billing_zip'] != df['shipping_zip']).astype(int)
    df['foreign_ip']     = (df['ip_country'].fillna('US') != 'US').astype(int)
    df['ship_delay']     = (df['actual_days'].fillna(0) - df['promised_days'].fillna(0))
    df['tax_ratio']      = df['tax_amount'].fillna(0) / (df['order_subtotal'].fillna(1) + 1e-9)
    df['shipping_ratio'] = df['shipping_fee'].fillna(0) / (df['order_subtotal'].fillna(1) + 1e-9)

    # Fill promo_code NaN
    df['promo_code'] = df['promo_code'].fillna('none')

    return df


def risk_label(prob: float) -> str:
    if prob >= 0.7:
        return 'HIGH'
    elif prob >= 0.4:
        return 'MEDIUM'
    return 'LOW'


def score_orders(model_path: str, pg_dsn: str, model_version: str | None = None) -> int:
    """
    Load the model, score all orders, write to ml_scores.
    Returns the number of orders scored.
    """
    print(f"[{datetime.now():%H:%M:%S}] Loading model from: {model_path}")
    pipe = joblib.load(model_path)

    # Infer model version from file if not supplied
    if model_version is None:
        import os
        mtime = os.path.getmtime(model_path)
        model_version = datetime.fromtimestamp(mtime).strftime('%Y-%m-%d')

    print(f"[{datetime.now():%H:%M:%S}] Connecting to Supabase...")
    conn = psycopg2.connect(pg_dsn)

    try:
        with conn.cursor() as cur:
            print(f"[{datetime.now():%H:%M:%S}] Fetching orders...")
            cur.execute(FETCH_QUERY)
            rows = cur.fetchall()
            cols = [desc[0] for desc in cur.description]

        df_raw = pd.DataFrame(rows, columns=cols)
        print(f"[{datetime.now():%H:%M:%S}] Fetched {len(df_raw):,} orders")

        df = engineer_features(df_raw)

        # Verify all expected feature columns are present
        missing = [c for c in ALL_FEATURES if c not in df.columns]
        if missing:
            print(f"ERROR: Missing feature columns: {missing}", file=sys.stderr)
            sys.exit(1)

        X = df[ALL_FEATURES].copy()

        print(f"[{datetime.now():%H:%M:%S}] Running inference...")
        probas = pipe.predict_proba(X)[:, 1]

        now = datetime.now(timezone.utc)
        scored_at = now.replace(tzinfo=None)  # store as naive UTC for Npgsql compat

        records = [
            (
                int(df_raw.iloc[i]['order_id']),
                round(float(probas[i]), 6),
                risk_label(probas[i]),
                model_version,
                scored_at
            )
            for i in range(len(df_raw))
        ]

        print(f"[{datetime.now():%H:%M:%S}] Writing {len(records):,} scores to ml_scores...")
        with conn.cursor() as cur:
            psycopg2.extras.execute_values(cur, UPSERT_QUERY, records, page_size=500)
        conn.commit()

        high   = sum(1 for r in records if r[2] == 'HIGH')
        medium = sum(1 for r in records if r[2] == 'MEDIUM')
        low    = sum(1 for r in records if r[2] == 'LOW')
        print(f"[{datetime.now():%H:%M:%S}] Done! HIGH={high:,}  MEDIUM={medium:,}  LOW={low:,}")
        return len(records)

    finally:
        conn.close()


def main():
    parser = argparse.ArgumentParser(description="Score live orders with fraud detection model")
    parser.add_argument('--model', default='fraud_detection_pipeline.pkl',
                        help='Path to trained model .pkl file')
    parser.add_argument('--pg', required=True,
                        help='PostgreSQL DSN (Supabase connection string)')
    parser.add_argument('--version', default=None,
                        help='Model version label (default: file modification date)')
    args = parser.parse_args()

    count = score_orders(args.model, args.pg, args.version)
    print(f"\nSuccessfully scored {count:,} orders.")


if __name__ == '__main__':
    main()
