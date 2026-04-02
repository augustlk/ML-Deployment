#!/usr/bin/env python3
"""
import_sqlite_data.py
=====================
One-time script to migrate data from shop.db (SQLite) into Supabase (PostgreSQL).

Prerequisites:
    pip install psycopg2-binary   # or psycopg2

Usage:
    python supabase/import_sqlite_data.py \
        --sqlite  src/ShopApp.Web/Data/shop.db \
        --pg      "Host=db.YOUR_REF.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PW;SSL Mode=Require"

    # Or set the PG connection string via environment variable:
    export DATABASE_URL="Host=..."
    python supabase/import_sqlite_data.py --sqlite src/ShopApp.Web/Data/shop.db

The script:
  1. Reads every table from SQLite in dependency order
  2. Inserts rows into PostgreSQL using COPY for performance
  3. Resets all SERIAL sequences so they continue from the right value
  4. Is safe to re-run — it TRUNCATEs tables before inserting (use --no-truncate to skip)
"""

import argparse
import os
import sqlite3
import sys
from io import StringIO
from typing import Any

try:
    import psycopg2
    import psycopg2.extras
except ImportError:
    sys.exit("psycopg2 not found. Run:  pip install psycopg2-binary")

# ── Tables in dependency order (parents before children) ──────────────────────
TABLE_ORDER = [
    "customers",
    "products",
    "orders",
    "order_items",
    "shipments",
    "product_reviews",
]

# ── Column-level converters: SQLite TEXT → PostgreSQL typed value ──────────────
# These convert the string datetime values stored in SQLite to ISO format that
# PostgreSQL accepts. Extend this dict if you add new tables/columns.
DATE_COLUMNS = {
    "customers":       ["birthdate", "created_at"],
    "orders":          ["order_datetime"],
    "shipments":       ["ship_datetime"],
    "product_reviews": ["review_datetime"],
}


def normalize_value(table: str, col: str, value: Any) -> Any:
    """Convert SQLite TEXT datetime values to a format PostgreSQL accepts."""
    if value is None:
        return None
    if col in DATE_COLUMNS.get(table, []):
        # SQLite stores dates as "YYYY-MM-DD HH:MM:SS" or "YYYY-MM-DD"
        # PostgreSQL accepts those formats directly — no conversion needed.
        return value
    return value


def pg_connection_string_from_npgsql(npgsql: str) -> str:
    """
    Convert an Npgsql/ADO.NET connection string to a psycopg2 DSN.

    Input:  Host=xxx;Port=5432;Database=postgres;Username=postgres;Password=pw;SSL Mode=Require
    Output: host=xxx port=5432 dbname=postgres user=postgres password=pw sslmode=require
    """
    mapping = {
        "host": "host",
        "port": "port",
        "database": "dbname",
        "username": "user",
        "password": "password",
        "ssl mode": "sslmode",
        "sslmode": "sslmode",
    }
    parts = []
    for kv in npgsql.split(";"):
        kv = kv.strip()
        if not kv:
            continue
        if "=" not in kv:
            continue
        key, _, val = kv.partition("=")
        pg_key = mapping.get(key.strip().lower())
        if pg_key:
            # sslmode values: "Require" → "require"
            parts.append(f"{pg_key}={val.strip().lower() if pg_key == 'sslmode' else val.strip()}")
    return " ".join(parts)


def migrate(sqlite_path: str, pg_dsn: str, truncate: bool = True) -> None:
    sq = sqlite3.connect(sqlite_path)
    sq.row_factory = sqlite3.Row

    # Accept both Npgsql-style and psycopg2-style connection strings
    if "Host=" in pg_dsn or "host=" in pg_dsn.split(";")[0]:
        pg_dsn = pg_connection_string_from_npgsql(pg_dsn)

    pg = psycopg2.connect(pg_dsn)
    pg.autocommit = False
    cur = pg.cursor()

    print(f"Connected to SQLite: {sqlite_path}")
    print(f"Connected to PostgreSQL: {pg_dsn[:60]}...")
    print()

    for table in TABLE_ORDER:
        print(f"  Migrating {table}...", end=" ", flush=True)

        rows = sq.execute(f"SELECT * FROM {table}").fetchall()
        if not rows:
            print("(empty)")
            continue

        cols = [d[0] for d in sq.execute(f"SELECT * FROM {table} LIMIT 0").description]

        if truncate:
            cur.execute(f"TRUNCATE TABLE {table} RESTART IDENTITY CASCADE")

        # Use psycopg2 copy_from for bulk insert performance
        buf = StringIO()
        for row in rows:
            values = []
            for col, val in zip(cols, row):
                val = normalize_value(table, col, val)
                if val is None:
                    values.append(r"\N")
                else:
                    # Escape backslashes and newlines for COPY text format
                    values.append(str(val).replace("\\", "\\\\").replace("\n", "\\n").replace("\t", "\\t"))
            buf.write("\t".join(values) + "\n")

        buf.seek(0)
        cur.copy_from(buf, table, columns=cols, null=r"\N")

        print(f"{len(rows):,} rows")

    # Reset sequences so new INSERTs get correct next-IDs
    print("\n  Resetting sequences...")
    for table in TABLE_ORDER:
        # Find the primary key column name
        pk_row = sq.execute(
            f"SELECT name FROM pragma_table_info('{table}') WHERE pk = 1"
        ).fetchone()
        if pk_row:
            pk_col = pk_row[0]
            cur.execute(
                f"SELECT setval(pg_get_serial_sequence('{table}', '{pk_col}'), "
                f"COALESCE(MAX({pk_col}), 1)) FROM {table}"
            )
            print(f"    {table}.{pk_col} sequence reset")

    pg.commit()
    print("\nMigration complete.")

    sq.close()
    cur.close()
    pg.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Migrate shop.db → Supabase PostgreSQL")
    parser.add_argument("--sqlite", default="src/ShopApp.Web/Data/shop.db",
                        help="Path to shop.db (default: src/ShopApp.Web/Data/shop.db)")
    parser.add_argument("--pg",
                        default=os.environ.get("DATABASE_URL", ""),
                        help="PostgreSQL/Supabase connection string (or set DATABASE_URL env var)")
    parser.add_argument("--no-truncate", action="store_true",
                        help="Skip TRUNCATE — append rows instead of replacing them")
    args = parser.parse_args()

    if not args.pg:
        sys.exit(
            "Error: supply a PostgreSQL connection string via --pg or DATABASE_URL env var.\n"
            "Example: --pg 'Host=db.xxx.supabase.co;Port=5432;Database=postgres;"
            "Username=postgres;Password=pw;SSL Mode=Require'"
        )

    migrate(args.sqlite, args.pg, truncate=not args.no_truncate)
