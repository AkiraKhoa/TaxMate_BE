-- Soft-delete support for Products (+ backfill missing stock columns if absent).
-- Applied via EF migration 20260816161157_AddProductIsDeleted (idempotent).

ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "CostPrice" numeric(18,6) NULL;
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "StockQuantity" numeric(18,4) NULL;
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT false;
ALTER TABLE "Ingredients" ADD COLUMN IF NOT EXISTS "StockQuantity" numeric(18,4) NOT NULL DEFAULT 0;
