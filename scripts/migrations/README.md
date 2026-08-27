# Legacy migration-lineage bridge

`bridge_legacy_lineage_to_repo.sql` is a one-time deployment bridge for the
reviewed local/legacy TaxMate lineage. It lets EF continue at M0 without
attempting to recreate the already-equivalent baseline schema.

The script is deliberately fail-closed. In one PostgreSQL transaction and
under an advisory transaction lock, it verifies:

- the exact five legacy/policy history rows and their EF product versions;
- that history contains no unknown, partial alias, or partial M0-M8 state;
- the full pre-M0 (or post-M8 on a rerun) public table/column/constraint/index
  catalog fingerprint;
- the two known physical legacy columns, the category/policy seed identities,
  and every TaxPeriods data precondition enforced by M0.

Only after all checks pass does it insert the three repository aliases. It
does not alter business schema or business data. Configured tax-threshold
amounts are intentionally preserved; their stable seed IDs, types, dates, and
positive values are checked instead.

## Run

Back up the target first. From `TaxMate_BE`, set a libpq connection string for
the target database and run the bridge with `psql` error-stop enabled:

```powershell
$env:TAXMATE_BRIDGE_DATABASE_URL = 'postgresql://postgres:password@host:5432/taxmate_db'
psql $env:TAXMATE_BRIDGE_DATABASE_URL -X -v ON_ERROR_STOP=1 -f scripts/migrations/bridge_legacy_lineage_to_repo.sql
```

After the bridge commits, point EF at the same database and apply M0-M8:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=host;Port=5432;Database=taxmate_db;Username=postgres;Password=password'
dotnet ef database update --project src/TaxMate.Model --startup-project src/TaxMate.API
```

M6 adds source-line idempotency for inventory movements. It raises before
creating either index if duplicate `(MovementType, ReferenceId, ProductId)` or
`(MovementType, ReferenceId, IngredientId)` rows already exist; it never
deduplicates data. The source coordinator must aggregate repeated BOM or
purchase lines for the same item before inserting the movement.

M7 normalizes every tax period strictly from its type/year/month/quarter
identity. Bangkok-local midnight is stored as the corresponding naive UTC
instant (`local - 07:00`), and the end is the next local boundary, exclusive.
Its `Down` cannot recover arbitrary historical timestamp values; it
deterministically restores the former UTC-midnight, inclusive-23:59:59
convention and refuses unsupported identity shapes.

M8 adds the nullable, UTC-naive `BusinessProfiles.InventoryInitializedAt`
cutover marker without backfilling it. It also makes each non-null
`PaymentAccounts.SePayBankAccountXid` globally unique. Existing duplicate XIDs
make M8 fail before any schema change; the migration does not rewrite account
data.

Rerunning both commands immediately is safe: the bridge emits a no-change
notice after revalidating the complete post-M8 state, and EF reports no pending
migrations.

A fresh database created from this repository does **not** use the bridge; run
the EF update command directly. Any unknown/partial lineage, changed catalog,
missing seed invariant, or unsafe TaxPeriods data makes the bridge raise an
exception and roll back without inserting aliases. Investigate that database
manually rather than changing or bypassing the checks.
