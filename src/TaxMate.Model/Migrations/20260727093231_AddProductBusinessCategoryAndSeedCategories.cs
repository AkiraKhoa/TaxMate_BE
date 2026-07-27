using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBusinessCategoryAndSeedCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: SeedTestData may have already patched local DBs with raw SQL.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'Products'
                          AND column_name = 'BusinessCategoryId'
                    ) THEN
                        ALTER TABLE "Products"
                        ADD COLUMN "BusinessCategoryId" uuid NULL;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "BusinessCategories"
                    ("BusinessCategoryId", "Code", "CreatedAt", "Description", "Name", "PitRate", "UpdatedAt", "VatRate")
                VALUES
                    ('a0000001-0000-4000-8000-000000000001', 'DIST_GOODS', TIMESTAMPTZ '2026-01-01 00:00:00+00', 'GTGT 1%, TNCN 0.5%', 'Phân phối, cung cấp hàng hóa', 0.5, TIMESTAMPTZ '2026-01-01 00:00:00+00', 1),
                    ('a0000001-0000-4000-8000-000000000002', 'PROD_TRANSPORT', TIMESTAMPTZ '2026-01-01 00:00:00+00', 'GTGT 3%, TNCN 1.5%', 'Sản xuất, vận tải, dịch vụ gắn HH, XD có NVL', 1.5, TIMESTAMPTZ '2026-01-01 00:00:00+00', 3),
                    ('a0000001-0000-4000-8000-000000000003', 'SERVICE_CONSTRUCT', TIMESTAMPTZ '2026-01-01 00:00:00+00', 'GTGT 5%, TNCN 2%', 'Dịch vụ, XD không bao thầu NVL', 2, TIMESTAMPTZ '2026-01-01 00:00:00+00', 5),
                    ('a0000001-0000-4000-8000-000000000004', 'ASSET_INSURANCE', TIMESTAMPTZ '2026-01-01 00:00:00+00', 'GTGT 5%, TNCN 5%', 'Cho thuê tài sản / đại lý BH, xổ số, BHĐC…', 5, TIMESTAMPTZ '2026-01-01 00:00:00+00', 5),
                    ('a0000001-0000-4000-8000-000000000005', 'OTHER', TIMESTAMPTZ '2026-01-01 00:00:00+00', 'GTGT 2%, TNCN 1%', 'Hoạt động khác', 1, TIMESTAMPTZ '2026-01-01 00:00:00+00', 2)
                ON CONFLICT ("BusinessCategoryId") DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE indexname = 'IX_Products_BusinessCategoryId'
                    ) THEN
                        CREATE INDEX "IX_Products_BusinessCategoryId"
                        ON "Products" ("BusinessCategoryId");
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE indexname = 'IX_Products_BusinessId_BusinessCategoryId'
                    ) THEN
                        CREATE INDEX "IX_Products_BusinessId_BusinessCategoryId"
                        ON "Products" ("BusinessId", "BusinessCategoryId");
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Products_BusinessCategories_BusinessCategoryId'
                    ) THEN
                        ALTER TABLE "Products"
                        ADD CONSTRAINT "FK_Products_BusinessCategories_BusinessCategoryId"
                        FOREIGN KEY ("BusinessCategoryId")
                        REFERENCES "BusinessCategories" ("BusinessCategoryId")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Products"
                DROP CONSTRAINT IF EXISTS "FK_Products_BusinessCategories_BusinessCategoryId";

                DROP INDEX IF EXISTS "IX_Products_BusinessCategoryId";
                DROP INDEX IF EXISTS "IX_Products_BusinessId_BusinessCategoryId";

                DELETE FROM "BusinessCategories"
                WHERE "BusinessCategoryId" IN (
                    'a0000001-0000-4000-8000-000000000001',
                    'a0000001-0000-4000-8000-000000000002',
                    'a0000001-0000-4000-8000-000000000003',
                    'a0000001-0000-4000-8000-000000000004',
                    'a0000001-0000-4000-8000-000000000005'
                );

                ALTER TABLE "Products"
                DROP COLUMN IF EXISTS "BusinessCategoryId";
                """);
        }
    }
}
