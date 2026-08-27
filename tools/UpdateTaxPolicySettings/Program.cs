using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using TaxMate.Model.Data;

// TaxMate — migrate annual policies to independently effective-dated thresholds.
// This tool is idempotent and preserves existing threshold values.

var solutionDir = new DirectoryInfo(AppContext.BaseDirectory);
while (solutionDir != null &&
       !File.Exists(Path.Combine(solutionDir.FullName, "TaxMate.sln")))
{
    solutionDir = solutionDir.Parent;
}

if (solutionDir is null)
{
    throw new InvalidOperationException("Could not locate TaxMate.sln.");
}

var apiDir = Path.Combine(solutionDir.FullName, "src", "TaxMate.API");
var config = new ConfigurationBuilder()
    .SetBasePath(apiDir)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var connectionCandidates = new[]
{
    new
    {
        Source = "TAXMATE_DB_CONNECTION_STRING",
        Value = Environment.GetEnvironmentVariable(
            "TAXMATE_DB_CONNECTION_STRING")
    },
    new
    {
        Source = "ConnectionStrings__DefaultConnection",
        Value = Environment.GetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection")
    },
    new
    {
        Source = "POSTGRESQLCONNSTR_DefaultConnection",
        Value = Environment.GetEnvironmentVariable(
            "POSTGRESQLCONNSTR_DefaultConnection")
    },
    new
    {
        Source = "CUSTOMCONNSTR_DefaultConnection",
        Value = Environment.GetEnvironmentVariable(
            "CUSTOMCONNSTR_DefaultConnection")
    },
    new
    {
        Source = "src/TaxMate.API/appsettings.json",
        Value = config.GetConnectionString("DefaultConnection")
    }
};

var selectedConnection = connectionCandidates
    .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.Value));
if (selectedConnection is null ||
    string.IsNullOrWhiteSpace(selectedConnection.Value))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured.");
}

var connectionString = selectedConnection.Value;
var connectionInfo = new NpgsqlConnectionStringBuilder(connectionString);
Console.WriteLine($"Connection source: {selectedConnection.Source}");
Console.WriteLine(
    $"Target database: {connectionInfo.Host}:{connectionInfo.Port}/" +
    connectionInfo.Database);

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var db = new AppDbContext(options);
await using var transaction = await db.Database.BeginTransactionAsync();

Console.WriteLine("Migrating tax thresholds to effective dates...");

await db.Database.ExecuteSqlRawAsync("""
    CREATE TABLE IF NOT EXISTS "TaxThresholdSettings"
    (
        "Id" uuid NOT NULL,
        "Type" character varying(50) NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "EffectiveFrom" date NOT NULL,
        "UpdatedByUserId" uuid NULL,
        "CreatedAt" timestamp without time zone NOT NULL,
        "UpdatedAt" timestamp without time zone NOT NULL,
        CONSTRAINT "PK_TaxThresholdSettings" PRIMARY KEY ("Id")
    );

    CREATE UNIQUE INDEX IF NOT EXISTS
        "IX_TaxThresholdSettings_Type_EffectiveFrom"
        ON "TaxThresholdSettings" ("Type", "EffectiveFrom");

    DO $migration$
    BEGIN
        IF EXISTS
        (
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = current_schema()
              AND table_name = 'TaxPolicySettings'
        ) THEN
            INSERT INTO "TaxThresholdSettings"
            (
                "Id", "Type", "Amount", "EffectiveFrom",
                "UpdatedByUserId", "CreatedAt", "UpdatedAt"
            )
            SELECT
                gen_random_uuid(),
                'AnnualRevenueTax',
                "AnnualRevenueThreshold",
                make_date("Year", 1, 1),
                "UpdatedByUserId",
                "CreatedAt",
                "UpdatedAt"
            FROM "TaxPolicySettings"
            ON CONFLICT ("Type", "EffectiveFrom") DO NOTHING;

            INSERT INTO "TaxThresholdSettings"
            (
                "Id", "Type", "Amount", "EffectiveFrom",
                "UpdatedByUserId", "CreatedAt", "UpdatedAt"
            )
            SELECT
                gen_random_uuid(),
                'EInvoiceRequirement',
                "EInvoiceRevenueThreshold",
                make_date("Year", 1, 1),
                "UpdatedByUserId",
                "CreatedAt",
                "UpdatedAt"
            FROM "TaxPolicySettings"
            ON CONFLICT ("Type", "EffectiveFrom") DO NOTHING;
        END IF;
    END
    $migration$;

    UPDATE "TaxThresholdSettings"
    SET "Id" = '20260000-0000-4000-a000-000000000011'
    WHERE "Type" = 'AnnualRevenueTax'
      AND "EffectiveFrom" = DATE '2026-01-01'
      AND NOT EXISTS
      (
          SELECT 1
          FROM "TaxThresholdSettings"
          WHERE "Id" = '20260000-0000-4000-a000-000000000011'
      );

    UPDATE "TaxThresholdSettings"
    SET "Id" = '20260000-0000-4000-a000-000000000012'
    WHERE "Type" = 'EInvoiceRequirement'
      AND "EffectiveFrom" = DATE '2026-01-01'
      AND NOT EXISTS
      (
          SELECT 1
          FROM "TaxThresholdSettings"
          WHERE "Id" = '20260000-0000-4000-a000-000000000012'
      );

    INSERT INTO "TaxThresholdSettings"
    (
        "Id", "Type", "Amount", "EffectiveFrom",
        "UpdatedByUserId", "CreatedAt", "UpdatedAt"
    )
    VALUES
    (
        '20260000-0000-4000-a000-000000000011',
        'AnnualRevenueTax',
        1000000000,
        DATE '2026-01-01',
        NULL,
        (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
        (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
    ),
    (
        '20260000-0000-4000-a000-000000000012',
        'EInvoiceRequirement',
        1000000000,
        DATE '2026-01-01',
        NULL,
        (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
        (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
    )
    ON CONFLICT ("Type", "EffectiveFrom") DO NOTHING;

    DROP TABLE IF EXISTS "TaxPolicySettings";

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    SELECT '20260820170000_UseEffectiveDatedTaxThresholds', '10.0.8'
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM "__EFMigrationsHistory"
        WHERE "MigrationId" =
            '20260820170000_UseEffectiveDatedTaxThresholds'
    );

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    SELECT '20260820173000_SeedDefaultTaxThresholdSettings', '10.0.8'
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM "__EFMigrationsHistory"
        WHERE "MigrationId" =
            '20260820173000_SeedDefaultTaxThresholdSettings'
    );
    """);

await transaction.CommitAsync();

var thresholds = await db.TaxThresholdSettings
    .AsNoTracking()
    .OrderBy(x => x.Type)
    .ThenBy(x => x.EffectiveFrom)
    .ToListAsync();

Console.WriteLine("Tax thresholds are ready:");
foreach (var threshold in thresholds)
{
    Console.WriteLine(
        $"  {threshold.Type}: {threshold.Amount:N0} VND " +
        $"from {threshold.EffectiveFrom:yyyy-MM-dd}");
}

Console.WriteLine("Existing threshold values and other data were preserved.");
