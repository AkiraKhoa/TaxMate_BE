\set ON_ERROR_STOP on

-- One-time, fail-closed bridge for the only legacy TaxMate migration lineage
-- recognized by this repository. This script never creates or alters business
-- schema/data. It records three repository aliases only after proving that the
-- legacy database is at the equivalent pre-M0 schema state.

BEGIN;

SELECT pg_advisory_xact_lock(
    hashtextextended('TaxMate.LegacyMigrationLineageBridge.v1', 0)
);

DO $bridge$
DECLARE
    v_alias_count integer;
    v_wave_count integer;
    v_missing text[];
    v_bad text[];
    v_table_count bigint;
    v_column_count bigint;
    v_constraint_count bigint;
    v_index_count bigint;
    v_table_fingerprint text;
    v_column_fingerprint text;
    v_constraint_fingerprint text;
    v_index_fingerprint text;
    v_default text;
    v_attnum smallint;
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION
            'Lineage bridge refused: public."__EFMigrationsHistory" does not exist.';
    END IF;

    -- Reject every history row outside the one reviewed legacy-to-M5 chain,
    -- and reject a known ID recorded with a different EF ProductVersion.
    WITH expected("MigrationId", "ProductVersion") AS
    (
        VALUES
            ('20260731065526_InitialCreate', '10.0.8'),
            ('20260814055212_AddIsStockTrackingEnabledToBusinessProfile', '10.0.0'),
            ('20260820153000_AddTaxPolicySettings', '10.0.8'),
            ('20260820170000_UseEffectiveDatedTaxThresholds', '10.0.8'),
            ('20260820173000_SeedDefaultTaxThresholdSettings', '10.0.8'),
            ('20260817082340_InitialMigrate', '10.0.8'),
            ('20260817083200_UpdateCategoryNames', '10.0.8'),
            ('20260817143600_AddRevenueThresholdAlerts', '10.0.8'),
            ('20260824110513_M0RepairTaxPeriodSchema', '10.0.8'),
            ('20260824110644_M1AddAccountingSourceMetadata', '10.0.8'),
            ('20260824111047_M2AddTaxProfileAndArtifactIdentity', '10.0.8'),
            ('20260824111235_M3AddInventoryLedger', '10.0.8'),
            ('20260824111447_M4AddCashBankMoneyLedger', '10.0.8'),
            ('20260824111623_M5AddQttCalculationSnapshots', '10.0.8'),
            ('20260824114829_M6AddInventoryMovementSourceIdempotency', '10.0.8'),
            ('20260824121306_M7NormalizeTaxPeriodBangkokBoundaries', '10.0.8'),
            ('20260824152737_M8AddInventoryCutoverAndSePayXidUniqueness', '10.0.8')
    )
    SELECT array_agg(
        h."MigrationId" || ' (recorded=' || h."ProductVersion" ||
        ', expected=' || COALESCE(e."ProductVersion", '<unknown>') || ')'
        ORDER BY h."MigrationId"
    )
    INTO v_bad
    FROM "__EFMigrationsHistory" h
    LEFT JOIN expected e ON e."MigrationId" = h."MigrationId"
    WHERE e."MigrationId" IS NULL
       OR h."ProductVersion" <> e."ProductVersion";

    IF COALESCE(cardinality(v_bad), 0) <> 0 THEN
        RAISE EXCEPTION
            'Lineage bridge refused: unknown migration IDs or ProductVersion mismatch: %',
            array_to_string(v_bad, ', ');
    END IF;

    -- These five rows identify the reviewed legacy lineage and prove that its
    -- later policy migrations have all completed.
    WITH required("MigrationId") AS
    (
        VALUES
            ('20260731065526_InitialCreate'),
            ('20260814055212_AddIsStockTrackingEnabledToBusinessProfile'),
            ('20260820153000_AddTaxPolicySettings'),
            ('20260820170000_UseEffectiveDatedTaxThresholds'),
            ('20260820173000_SeedDefaultTaxThresholdSettings')
    )
    SELECT array_agg(r."MigrationId" ORDER BY r."MigrationId")
    INTO v_missing
    FROM required r
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM "__EFMigrationsHistory" h
        WHERE h."MigrationId" = r."MigrationId"
    );

    IF COALESCE(cardinality(v_missing), 0) <> 0 THEN
        RAISE EXCEPTION
            'Lineage bridge refused: this is not the complete known legacy lineage; missing: %',
            array_to_string(v_missing, ', ');
    END IF;

    SELECT count(*)
    INTO v_alias_count
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" IN
    (
        '20260817082340_InitialMigrate',
        '20260817083200_UpdateCategoryNames',
        '20260817143600_AddRevenueThresholdAlerts'
    );

    IF v_alias_count NOT IN (0, 3) THEN
        RAISE EXCEPTION
            'Lineage bridge refused: repository aliases are partial (% of 3).',
            v_alias_count;
    END IF;

    SELECT count(*)
    INTO v_wave_count
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" IN
    (
        '20260824110513_M0RepairTaxPeriodSchema',
        '20260824110644_M1AddAccountingSourceMetadata',
        '20260824111047_M2AddTaxProfileAndArtifactIdentity',
        '20260824111235_M3AddInventoryLedger',
        '20260824111447_M4AddCashBankMoneyLedger',
        '20260824111623_M5AddQttCalculationSnapshots',
        '20260824114829_M6AddInventoryMovementSourceIdempotency',
        '20260824121306_M7NormalizeTaxPeriodBangkokBoundaries',
        '20260824152737_M8AddInventoryCutoverAndSePayXidUniqueness'
    );

    IF v_wave_count NOT IN (0, 9) THEN
        RAISE EXCEPTION
            'Lineage bridge refused: M0-M8 history is partial (% of 9).',
            v_wave_count;
    END IF;

    IF v_alias_count = 0 AND v_wave_count <> 0 THEN
        RAISE EXCEPTION
            'Lineage bridge refused: M0-M8 exists without all repository aliases.';
    END IF;

    -- The two physical/default differences below are the exact known legacy
    -- shape. They distinguish it from a fresh repository-created database.
    SELECT a.attnum, pg_get_expr(d.adbin, d.adrelid)
    INTO v_attnum, v_default
    FROM pg_attribute a
    JOIN pg_class c ON c.oid = a.attrelid
    JOIN pg_namespace n ON n.oid = c.relnamespace
    LEFT JOIN pg_attrdef d
        ON d.adrelid = a.attrelid AND d.adnum = a.attnum
    WHERE n.nspname = 'public'
      AND c.relname = 'BusinessProfiles'
      AND a.attname = 'IsStockTrackingEnabled'
      AND a.attnum > 0
      AND NOT a.attisdropped;

    IF v_attnum IS DISTINCT FROM 19 OR v_default IS DISTINCT FROM 'true' THEN
        RAISE EXCEPTION
            'Lineage bridge refused: BusinessProfiles.IsStockTrackingEnabled is not the known legacy column (attnum %, default %).',
            v_attnum, v_default;
    END IF;

    SELECT a.attnum, pg_get_expr(d.adbin, d.adrelid)
    INTO v_attnum, v_default
    FROM pg_attribute a
    JOIN pg_class c ON c.oid = a.attrelid
    JOIN pg_namespace n ON n.oid = c.relnamespace
    LEFT JOIN pg_attrdef d
        ON d.adrelid = a.attrelid AND d.adnum = a.attnum
    WHERE n.nspname = 'public'
      AND c.relname = 'Products'
      AND a.attname = 'IsDeleted'
      AND a.attnum > 0
      AND NOT a.attisdropped;

    IF v_attnum IS DISTINCT FROM 15 OR v_default IS DISTINCT FROM 'false' THEN
        RAISE EXCEPTION
            'Lineage bridge refused: Products.IsDeleted is not the known legacy column (attnum %, default %).',
            v_attnum, v_default;
    END IF;

    -- Seed/data invariants owned by the aliased and policy migrations. Tax
    -- threshold amounts are user-editable, so identity/type/date are asserted
    -- while the current positive configured amount is preserved.
    IF NOT EXISTS
    (
        SELECT 1 FROM "BusinessCategories"
        WHERE "BusinessCategoryId" = 'a0000001-0000-4000-8000-000000000003'
          AND "Name" = 'Dịch vụ'
    ) OR NOT EXISTS
    (
        SELECT 1 FROM "BusinessCategories"
        WHERE "BusinessCategoryId" = 'd1111111-1111-1111-1111-111111111111'
          AND "Name" = 'FNB'
    ) THEN
        RAISE EXCEPTION
            'Lineage bridge refused: UpdateCategoryNames seed invariants are missing.';
    END IF;

    IF NOT EXISTS
    (
        SELECT 1 FROM "TaxThresholdSettings"
        WHERE "Id" = '20260000-0000-4000-a000-000000000011'
          AND "Type" = 'AnnualRevenueTax'
          AND "EffectiveFrom" = DATE '2026-01-01'
          AND "Amount" > 0
    ) OR NOT EXISTS
    (
        SELECT 1 FROM "TaxThresholdSettings"
        WHERE "Id" = '20260000-0000-4000-a000-000000000012'
          AND "Type" = 'EInvoiceRequirement'
          AND "EffectiveFrom" = DATE '2026-01-01'
          AND "Amount" > 0
    ) THEN
        RAISE EXCEPTION
            'Lineage bridge refused: policy seed identity/type/date invariants are missing.';
    END IF;

    -- Catalog fingerprints cover every public application table, column,
    -- constraint, and index. The column fingerprint is semantic (ordered by
    -- name) and normalizes only the two legacy defaults checked exactly above.
    WITH application_tables AS
    (
        SELECT c.oid, c.relname
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relkind IN ('r', 'p')
          AND c.relname <> '__EFMigrationsHistory'
    )
    SELECT count(*),
           md5(COALESCE(string_agg(t.relname, E'\n' ORDER BY t.relname), ''))
    INTO v_table_count, v_table_fingerprint
    FROM application_tables t;

    WITH application_tables AS
    (
        SELECT c.oid, c.relname
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relkind IN ('r', 'p')
          AND c.relname <> '__EFMigrationsHistory'
    )
    SELECT count(*),
           md5(COALESCE(string_agg(
               format(
                   '%s|%s|%s|%s|%s|%s',
                   t.relname,
                   a.attname,
                   format_type(a.atttypid, a.atttypmod),
                   a.attnotnull,
                   CASE
                       WHEN (t.relname, a.attname) IN
                            (('BusinessProfiles', 'IsStockTrackingEnabled'),
                             ('Products', 'IsDeleted'))
                       THEN ''
                       ELSE COALESCE(pg_get_expr(d.adbin, d.adrelid), '')
                   END,
                   a.attidentity::text
               ),
               E'\n' ORDER BY t.relname, a.attname
           ), ''))
    INTO v_column_count, v_column_fingerprint
    FROM application_tables t
    JOIN pg_attribute a ON a.attrelid = t.oid
    LEFT JOIN pg_attrdef d
        ON d.adrelid = a.attrelid AND d.adnum = a.attnum
    WHERE a.attnum > 0
      AND NOT a.attisdropped;

    WITH application_tables AS
    (
        SELECT c.oid, c.relname
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relkind IN ('r', 'p')
          AND c.relname <> '__EFMigrationsHistory'
    )
    SELECT count(*),
           md5(COALESCE(string_agg(
               format('%s|%s|%s',
                   t.relname, con.conname,
                   pg_get_constraintdef(con.oid, true)),
               E'\n' ORDER BY t.relname, con.conname
           ), ''))
    INTO v_constraint_count, v_constraint_fingerprint
    FROM application_tables t
    JOIN pg_constraint con ON con.conrelid = t.oid;

    WITH application_tables AS
    (
        SELECT c.oid, c.relname
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relkind IN ('r', 'p')
          AND c.relname <> '__EFMigrationsHistory'
    )
    SELECT count(*),
           md5(COALESCE(string_agg(
               format('%s|%s|%s',
                   t.relname, index_class.relname,
                   pg_get_indexdef(index_class.oid)),
               E'\n' ORDER BY t.relname, index_class.relname
           ), ''))
    INTO v_index_count, v_index_fingerprint
    FROM application_tables t
    JOIN pg_index index_catalog ON index_catalog.indrelid = t.oid
    JOIN pg_class index_class ON index_class.oid = index_catalog.indexrelid;

    IF v_wave_count = 0 THEN
        IF (v_table_count, v_table_fingerprint) IS DISTINCT FROM
           (39::bigint, '0401ee208c1405f3b8c6ab340447368f'::text)
        OR (v_column_count, v_column_fingerprint) IS DISTINCT FROM
           (506::bigint, '9f3b81621fc5f58fb73e2fa601ad20a9'::text)
        OR (v_constraint_count, v_constraint_fingerprint) IS DISTINCT FROM
           (443::bigint, '36ce49b6e60dbf6134284143d5c689f9'::text)
        OR (v_index_count, v_index_fingerprint) IS DISTINCT FROM
           (135::bigint, '381949121b849baab29443d4e88d160b'::text) THEN
            RAISE EXCEPTION
                'Lineage bridge refused: pre-M0 catalog mismatch. tables=%/%, columns=%/%, constraints=%/%, indexes=%/%',
                v_table_count, v_table_fingerprint,
                v_column_count, v_column_fingerprint,
                v_constraint_count, v_constraint_fingerprint,
                v_index_count, v_index_fingerprint;
        END IF;

        -- Repeat every data preflight enforced by M0 before changing history.
        IF EXISTS
        (
            SELECT 1
            FROM "TaxPeriods"
            WHERE "BusinessProfileId" IS NOT NULL
              AND "BusinessProfileId" <> "BusinessId"
        ) THEN
            RAISE EXCEPTION
                'Lineage bridge refused: TaxPeriods has mismatched BusinessId/BusinessProfileId values.';
        END IF;

        IF EXISTS
        (
            SELECT 1
            FROM "TaxPeriods"
            GROUP BY "BusinessId", "Year",
                CASE WHEN "PeriodType" = 'Monthly' THEN "Month" END,
                CASE WHEN "PeriodType" = 'Quarterly' THEN "Quarter" END,
                "PeriodType"
            HAVING count(*) > 1
        ) THEN
            RAISE EXCEPTION
                'Lineage bridge refused: TaxPeriods has duplicate M0 period identities.';
        END IF;

        IF EXISTS
        (
            SELECT 1
            FROM "TaxPeriods"
            WHERE NOT
            (
                ("PeriodType" = 'Monthly'
                    AND "Month" BETWEEN 1 AND 12
                    AND "Quarter" IS NULL)
                OR ("PeriodType" = 'Quarterly'
                    AND "Month" IS NULL
                    AND "Quarter" BETWEEN 1 AND 4)
                OR ("PeriodType" = 'Yearly'
                    AND "Month" IS NULL
                    AND "Quarter" IS NULL)
            )
        ) THEN
            RAISE EXCEPTION
                'Lineage bridge refused: TaxPeriods has invalid M0 period shapes.';
        END IF;
    ELSE
        IF (v_table_count, v_table_fingerprint) IS DISTINCT FROM
           (41::bigint, '339aa92ef36a9b5530d2fcbc07f86393'::text)
        OR (v_column_count, v_column_fingerprint) IS DISTINCT FROM
           (561::bigint, '7bd76372b360f4d719b54c7bb115483e'::text)
        OR (v_constraint_count, v_constraint_fingerprint) IS DISTINCT FROM
           (505::bigint, '7e109ac91e8aa102c940dfa8ebcad050'::text)
        OR (v_index_count, v_index_fingerprint) IS DISTINCT FROM
           (158::bigint, 'e5e0088f2974ff8fb6ef088ea564ceca'::text) THEN
            RAISE EXCEPTION
                'Lineage bridge refused: post-M8 catalog mismatch. tables=%/%, columns=%/%, constraints=%/%, indexes=%/%',
                v_table_count, v_table_fingerprint,
                v_column_count, v_column_fingerprint,
                v_constraint_count, v_constraint_fingerprint,
                v_index_count, v_index_fingerprint;
        END IF;
    END IF;

    IF v_alias_count = 3 THEN
        RAISE NOTICE
            'Legacy lineage bridge already applied and consistent (M0-M8 count: %). No changes made.',
            v_wave_count;
        RETURN;
    END IF;

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES
        ('20260817082340_InitialMigrate', '10.0.8'),
        ('20260817083200_UpdateCategoryNames', '10.0.8'),
        ('20260817143600_AddRevenueThresholdAlerts', '10.0.8');

    RAISE NOTICE
        'Legacy lineage verified; inserted exactly 3 repository history aliases. Run EF database update for M0-M8.';
END
$bridge$;

COMMIT;
