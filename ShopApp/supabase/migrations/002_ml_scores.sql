-- ── ML Scores Table ───────────────────────────────────────────────────────────
-- Stores fraud probability scores produced by the ML pipeline.
-- The Python scoring script writes here after each model inference run.
-- The C# DatabaseScoringService reads from here to power the warehouse page.

CREATE TABLE IF NOT EXISTS ml_scores (
    order_id          INTEGER PRIMARY KEY REFERENCES orders(order_id) ON DELETE CASCADE,
    fraud_probability FLOAT   NOT NULL CHECK (fraud_probability BETWEEN 0 AND 1),
    risk_level        TEXT    NOT NULL DEFAULT 'LOW' CHECK (risk_level IN ('LOW','MEDIUM','HIGH')),
    model_version     TEXT,
    scored_at         TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_ml_scores_fraud_prob
    ON ml_scores (fraud_probability DESC);

CREATE INDEX IF NOT EXISTS idx_ml_scores_scored_at
    ON ml_scores (scored_at DESC);
