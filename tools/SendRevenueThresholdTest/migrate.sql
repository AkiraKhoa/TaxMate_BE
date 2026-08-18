CREATE TABLE IF NOT EXISTS "RevenueThresholdAlerts" (
    "Id" uuid NOT NULL,
    "OwnerId" uuid NOT NULL,
    "Year" integer NOT NULL,
    "Quarter" integer NOT NULL,
    "WindowStart" timestamp without time zone NOT NULL,
    "WindowEnd" timestamp without time zone NOT NULL,
    "TotalRevenue" numeric(18,2) NOT NULL,
    "SentAt" timestamp without time zone NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_RevenueThresholdAlerts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RevenueThresholdAlerts_Users_OwnerId" FOREIGN KEY ("OwnerId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_RevenueThresholdAlerts_OwnerId_Year"
    ON "RevenueThresholdAlerts" ("OwnerId", "Year");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260817143600_AddRevenueThresholdAlerts', '10.0.8'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260817143600_AddRevenueThresholdAlerts'
);
