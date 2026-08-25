using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Model.Entities;
using TaxMate.Repository.Repositories;
using TaxMate.Service.Common;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;
using Xunit.Sdk;

namespace TaxMate.Service.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public const string ConnectionEnvironmentVariable =
        "TAXMATE_TEST_POSTGRES_ADMIN_CONNECTION";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable)))
        {
            Skip = $"Set {ConnectionEnvironmentVariable} to an admin/test PostgreSQL connection.";
        }
    }
}

[Trait("Category", "PostgreSQL")]
public class PostgresAccountingIntegrationTests
{
    static PostgresAccountingIntegrationTests()
    {
        // Match the API host. Tax accounting instants are deliberately stored
        // in timestamp-without-time-zone columns as normalized naive UTC.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    [PostgresFact]
    public async Task AdvisoryTransactionLock_BlocksSecondTransactionUntilCommit()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        await firstContext.Database.BeginTransactionAsync();
        await secondContext.Database.BeginTransactionAsync();

        var ownerId = Guid.NewGuid();
        var firstLock = new AccountingTransactionLockRepository(firstContext);
        var secondLock = new AccountingTransactionLockRepository(secondContext);
        await firstLock.AcquireOwnerYearLocksAsync(ownerId, [2026]);

        var secondAcquire = secondLock.AcquireOwnerYearLocksAsync(ownerId, [2026]);
        await Task.Delay(250);
        Assert.False(secondAcquire.IsCompleted);
        Assert.NotNull(firstLock.CurrentTransactionId);
        Assert.NotEqual(firstLock.CurrentTransactionId, secondLock.CurrentTransactionId);

        await firstContext.Database.CommitTransactionAsync();
        Assert.Null(firstLock.CurrentTransactionId);
        await secondAcquire.WaitAsync(TimeSpan.FromSeconds(5));
        await secondContext.Database.CommitTransactionAsync();
    }

    [PostgresFact]
    public async Task CloseAndMutationGuard_UseSameOwnerYearLockAndFreshCanonicalState()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var ownerId = Guid.NewGuid();
        var activeBusinessId = Guid.NewGuid();
        var inactiveBusinessId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var sourceTransactionId = Guid.NewGuid();
        var expenseCategoryId = Guid.NewGuid();
        var (periodStart, periodEnd) =
            BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);

        await using (var seed = database.CreateContext())
        {
            var now = BangkokBusinessTime.NormalizeNaiveUtc(DateTime.UtcNow);
            seed.Users.Add(new User
            {
                Id = ownerId,
                Email = $"owner-{ownerId:N}@example.test",
                FullName = "Integration owner",
                CreatedAt = now,
                UpdatedAt = now
            });
            seed.BusinessProfiles.AddRange(
                new BusinessProfile
                {
                    Id = activeBusinessId,
                    OwnerId = ownerId,
                    BusinessName = "Active shop",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new BusinessProfile
                {
                    Id = inactiveBusinessId,
                    OwnerId = ownerId,
                    BusinessName = "Historical inactive shop",
                    IsActive = false,
                    CreatedAt = now.AddTicks(1),
                    UpdatedAt = now
                });
            seed.TaxPeriods.Add(new TaxPeriod
            {
                Id = periodId,
                BusinessId = inactiveBusinessId,
                PeriodType = TaxPeriodTypes.Quarterly,
                Year = 2026,
                Quarter = 1,
                PeriodStartDate = periodStart,
                PeriodEndDate = periodEnd,
                Status = TaxPeriodStatuses.Open,
                CreatedAt = now,
                UpdatedAt = now
            });
            seed.ExpenseCategories.Add(new ExpenseCategory
            {
                ExpenseCategoryId = expenseCategoryId,
                BusinessId = activeBusinessId,
                CategoryName = "Integration category",
                CreatedAt = now,
                UpdatedAt = now
            });
            seed.Transactions.AddRange(
                new Transaction
                {
                    TransactionId = sourceTransactionId,
                    BusinessId = activeBusinessId,
                    TransactionCode = $"IT-{Guid.NewGuid():N}",
                    TransactionDate = periodStart.AddDays(10),
                    CompletedAt = periodStart.AddDays(10),
                    SubTotal = 125_000m,
                    TotalAmount = 125_000m,
                    Status = "Completed",
                    TransactionType = TransactionTypes.Sale,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Transaction
                {
                    TransactionId = Guid.NewGuid(),
                    BusinessId = activeBusinessId,
                    TransactionCode = $"BOUNDARY-{Guid.NewGuid():N}",
                    TransactionDate = periodEnd,
                    CompletedAt = periodEnd,
                    SubTotal = 999_000m,
                    TotalAmount = 999_000m,
                    Status = "Completed",
                    TransactionType = TransactionTypes.Sale,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            seed.Expenses.AddRange(
                new Expense
                {
                    ExpenseId = Guid.NewGuid(),
                    BusinessId = activeBusinessId,
                    ExpenseCategoryId = expenseCategoryId,
                    VoucherNumber = "EXP-IN-PERIOD",
                    ExpenseTitle = "In-period expense",
                    Amount = 30_000m,
                    ExpenseDate = periodStart.AddDays(15),
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Expense
                {
                    ExpenseId = Guid.NewGuid(),
                    BusinessId = activeBusinessId,
                    ExpenseCategoryId = expenseCategoryId,
                    VoucherNumber = "EXP-NEXT-BOUNDARY",
                    ExpenseTitle = "Next-period boundary expense",
                    Amount = 888_000m,
                    ExpenseDate = periodEnd,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            await seed.SaveChangesAsync();
        }

        await using var mutationContext = database.CreateContext();
        await mutationContext.Database.BeginTransactionAsync();
        var scopeRepository = new AccountingScopeReadRepository(mutationContext);
        var scope = await scopeRepository.ResolveOwnerScopeAsync(activeBusinessId);
        Assert.NotNull(scope);
        Assert.Contains(activeBusinessId, scope!.BusinessIds);
        Assert.Contains(inactiveBusinessId, scope.BusinessIds);

        var mutationLock = new AccountingTransactionLockRepository(mutationContext);
        var mutationGuard = new TaxPeriodMutationGuard(
            scopeRepository,
            mutationLock);
        await mutationGuard.EnsureCanCreateAsync(
            ownerId,
            activeBusinessId,
            periodStart.AddDays(20));

        // Make a source mutation after taking the same owner/year lock and
        // keep it uncommitted while CloseAsync starts. A close implementation
        // that previews before acquiring the lock will snapshot 125,000 and
        // fail the 225,000 assertions below.
        var sourceTransaction = await mutationContext.Transactions
            .SingleAsync(x => x.TransactionId == sourceTransactionId);
        sourceTransaction.SubTotal = 225_000m;
        sourceTransaction.TotalAmount = 225_000m;
        await mutationContext.SaveChangesAsync();

        await using var closeContext = database.CreateContext();
        var closeRepository = new TaxPeriodRepository(closeContext);
        var identity = await closeRepository.GetIdentityAsync(periodId);
        Assert.NotNull(identity);
        Assert.Equal(ownerId, identity!.OwnerId);
        Assert.Equal(inactiveBusinessId, identity.BusinessId);

        var closeUnitOfWork = new UnitOfWork(closeContext);
        var closeService = new TaxPeriodService(
            closeRepository,
            new TaxCalculationRepository(closeContext),
            new Mock<ITaxPolicyService>().Object,
            closeUnitOfWork,
            new AccountingTransactionLockRepository(closeContext),
            new S2eBookProjector(new MoneyMovementRepository(closeContext)));

        var closeTask = closeService.CloseAsync(
            ownerId,
            periodId,
            new CloseTaxPeriodRequest { ConfirmWarnings = true });
        await Task.Delay(250);
        Assert.False(closeTask.IsCompleted);

        await mutationContext.Database.CommitTransactionAsync();
        var response = await closeTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TaxPeriodStatuses.Closed, response.Status);
        Assert.Equal(225_000m, response.SalesRevenue);

        await using var verify = database.CreateContext();
        var verifyRepository = new TaxPeriodRepository(verify);
        var stored = await verify.TaxPeriods
            .AsNoTracking()
            .SingleAsync(x => x.Id == periodId);
        Assert.Equal(TaxPeriodStatuses.Closed, stored.Status);
        Assert.Equal(225_000m, stored.TotalRevenue);

        var preview = await verifyRepository.GetPreviewAsync(periodId);
        Assert.NotNull(preview);
        Assert.Equal(1, preview!.TransactionCount);
        Assert.Equal(225_000m, preview.SalesRevenue);
        Assert.Equal(1, preview.ExpenseCount);
        Assert.Equal(30_000m, preview.TotalExpense);

        var detail = await verifyRepository.GetDetailAsync(periodId);
        Assert.NotNull(detail);
        Assert.Equal(1, detail!.TransactionCount);
        Assert.Equal(1, detail.ExpenseCount);
        Assert.Equal(30_000m, detail.TotalExpense);

        var directRevenue = await verifyRepository
            .GetRevenueForBusinessInPeriodAsync(
                activeBusinessId,
                periodStart,
                periodEnd);
        Assert.Equal(225_000m, directRevenue);
    }
}

internal sealed partial class TemporaryPostgresDatabase : IAsyncDisposable
{
    private const string DatabasePrefix = "taxmate_it_";
    private readonly string _adminConnectionString;
    private readonly string _databaseName;

    private TemporaryPostgresDatabase(
        string adminConnectionString,
        string databaseName,
        string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<TemporaryPostgresDatabase> CreateAsync()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgresFactAttribute.ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            throw SkipException.ForSkip(
                $"Set {PostgresFactAttribute.ConnectionEnvironmentVariable}.");
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        if (string.IsNullOrWhiteSpace(adminBuilder.Host) ||
            string.IsNullOrWhiteSpace(adminBuilder.Database) ||
            string.IsNullOrWhiteSpace(adminBuilder.Username))
        {
            throw new InvalidOperationException(
                "The PostgreSQL integration connection must explicitly name host, database, and username.");
        }

        var databaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
        EnsureSafeDatabaseName(databaseName);
        await using (var admin = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await create.ExecuteNonQueryAsync();
        }

        var testBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
            ApplicationName = "TaxMate PostgreSQL integration tests"
        };
        var database = new TemporaryPostgresDatabase(
            adminBuilder.ConnectionString,
            databaseName,
            testBuilder.ConnectionString);
        try
        {
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        EnsureSafeDatabaseName(_databaseName);
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using (var terminate = admin.CreateCommand())
        {
            terminate.CommandText =
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                "WHERE datname = @databaseName AND pid <> pg_backend_pid()";
            terminate.Parameters.AddWithValue("databaseName", _databaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\"";
        await drop.ExecuteNonQueryAsync();
    }

    private static void EnsureSafeDatabaseName(string databaseName)
    {
        if (!SafeTemporaryDatabaseName().IsMatch(databaseName))
        {
            throw new InvalidOperationException(
                "Refusing to create or drop an unverified PostgreSQL database name.");
        }
    }

    [GeneratedRegex("^taxmate_it_[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeTemporaryDatabaseName();
}
