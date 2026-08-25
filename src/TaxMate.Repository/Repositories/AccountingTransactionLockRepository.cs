using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public sealed class AccountingTransactionLockRepository
    : IAccountingTransactionLockRepository
{
    private readonly AppDbContext _dbContext;

    public AccountingTransactionLockRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public bool HasActiveTransaction =>
        _dbContext.Database.CurrentTransaction is not null;

    public Guid? CurrentTransactionId =>
        _dbContext.Database.CurrentTransaction?.TransactionId;

    public async Task AcquireOwnerYearLocksAsync(
        Guid ownerId,
        IReadOnlyCollection<int> years,
        CancellationToken cancellationToken = default)
    {
        if (!HasActiveTransaction)
        {
            throw new InvalidOperationException(
                "An active database transaction is required before acquiring accounting locks.");
        }

        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Owner id cannot be empty.", nameof(ownerId));
        }

        var orderedYears = years
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        if (orderedYears.Length == 0)
        {
            throw new ArgumentException("At least one year is required.", nameof(years));
        }

        foreach (var year in orderedYears)
        {
            var lockKey = CreateLockKey(ownerId, year);
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({lockKey})",
                cancellationToken);
        }
    }

    private static long CreateLockKey(Guid ownerId, int year)
    {
        Span<byte> input = stackalloc byte[20];
        ownerId.TryWriteBytes(input);
        BinaryPrimitives.WriteInt32LittleEndian(input[16..], year);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return BinaryPrimitives.ReadInt64LittleEndian(hash);
    }
}
