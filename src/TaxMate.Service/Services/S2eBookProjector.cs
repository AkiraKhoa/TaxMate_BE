using TaxMate.Model.Common;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public sealed class S2eBookProjector : IS2eBookProjector
{
    private readonly IMoneyMovementRepository _moneyMovements;

    public S2eBookProjector(IMoneyMovementRepository moneyMovements)
    {
        _moneyMovements = moneyMovements;
    }

    public async Task<S2eBookProjection> ProjectAsync(
        Guid ownerId,
        Guid businessId,
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken cancellationToken = default)
    {
        if (ownerId == Guid.Empty || businessId == Guid.Empty)
        {
            throw new BadRequestException("Owner and business are required.");
        }

        fromInclusive = BangkokBusinessTime.NormalizeNaiveUtc(fromInclusive);
        toExclusive = BangkokBusinessTime.NormalizeNaiveUtc(toExclusive);

        if (fromInclusive >= toExclusive)
        {
            throw new BadRequestException("The S2e date range must be a non-empty half-open range.");
        }

        var actualOwnerId = await _moneyMovements.GetBusinessOwnerIdAsync(
            businessId,
            cancellationToken);
        if (actualOwnerId is null)
        {
            throw new NotFoundException("Business profile not found.");
        }

        if (actualOwnerId != ownerId)
        {
            throw new ForbiddenException();
        }

        // These calls intentionally stay sequential: the scoped repository uses one DbContext.
        var accounts = await _moneyMovements.GetAccountsForBusinessAsync(
            businessId,
            cancellationToken);
        var movements = await _moneyMovements.GetMovementsForBusinessBeforeAsync(
            businessId,
            toExclusive,
            cancellationToken);
        var expectedSources = await _moneyMovements.GetExpectedSourcesBeforeAsync(
            businessId,
            toExclusive,
            cancellationToken);
        var systemIncomeIds = await _moneyMovements.GetSystemIncomeIdsBeforeAsync(
            businessId,
            toExclusive,
            cancellationToken);

        var movementsByAccount = movements
            .GroupBy(x => x.PaymentAccountId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<MoneyMovement>)x.ToList());
        var relevantAccounts = accounts
            .Where(account =>
            {
                movementsByAccount.TryGetValue(
                    account.PaymentAccountId,
                    out var accountMovements);
                return IsAccountRelevant(
                    account,
                    accountMovements ?? [],
                    toExclusive);
            })
            .ToList();
        var accountsById = relevantAccounts.ToDictionary(
            x => x.PaymentAccountId);
        var hasMissingInitialDate = relevantAccounts.Any(
            x => !x.InitialBalanceDate.HasValue);
        var businessAuditStart = hasMissingInitialDate
            ? DateTime.MinValue
            : relevantAccounts
                .Select(x => ToInitialBalanceInstant(x.InitialBalanceDate!.Value))
                .DefaultIfEmpty(DateTime.MinValue)
                .Min();

        DateTime GetAuditStart(Guid? paymentAccountId)
        {
            if (paymentAccountId.HasValue &&
                accountsById.TryGetValue(paymentAccountId.Value, out var account))
            {
                return account.InitialBalanceDate.HasValue
                    ? ToInitialBalanceInstant(account.InitialBalanceDate.Value)
                    : DateTime.MinValue;
            }

            return businessAuditStart;
        }

        var allMovementsBySource = movements
            .GroupBy(x => (x.MovementType, x.ReferenceId))
            .ToDictionary(x => x.Key, x => x.First());
        var auditedMovements = movements
            .Where(x => MovementInstant(x) >= GetAuditStart(x.PaymentAccountId))
            .ToList();
        var auditedExpectedSources = expectedSources
            .Where(expected =>
            {
                var accountId = expected.PaymentAccountId;
                if (!accountId.HasValue &&
                    allMovementsBySource.TryGetValue(
                        (expected.MovementType, expected.ReferenceId),
                        out var actual))
                {
                    accountId = actual.PaymentAccountId;
                }

                return BangkokBusinessTime.NormalizeNaiveUtc(
                           expected.MovementDate) >= GetAuditStart(accountId);
            })
            .ToList();

        var blockers = ValidateMovements(auditedMovements);
        ValidateSources(
            auditedMovements,
            auditedExpectedSources,
            systemIncomeIds,
            blockers);

        var sections = new List<S2eAccountSection>();

        foreach (var account in relevantAccounts)
        {
            movementsByAccount.TryGetValue(account.PaymentAccountId, out var accountMovements);
            accountMovements ??= [];

            ValidateAccount(account, fromInclusive, blockers);

            var validMovements = accountMovements
                .Where(x =>
                    x.Amount > 0 &&
                    MoneyMovementTypes.All.Contains(x.MovementType))
                .ToList();

            var calculation = CalculateAccount(
                account,
                fromInclusive,
                toExclusive,
                validMovements);
            var entries = validMovements
                .Where(x =>
                    MovementInstant(x) >= fromInclusive &&
                    MovementInstant(x) < toExclusive)
                .OrderBy(MovementInstant)
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.MoneyMovementId)
                .Select(ToEntry)
                .ToList();

            sections.Add(new S2eAccountSection
            {
                PaymentAccountId = account.PaymentAccountId,
                AccountType = account.AccountType,
                DisplayName = GetDisplayName(account),
                IsActive = account.IsActive,
                OpeningBalance = calculation.OpeningBalance,
                TotalIn = calculation.TotalIn,
                TotalOut = calculation.TotalOut,
                EndingBalance = calculation.EndingBalance,
                Entries = entries
            });
        }

        sections = sections
            .OrderBy(x => x.AccountType == PaymentAccountTypes.Cash ? 0 : 1)
            .ThenBy(x => x.DisplayName)
            .ToList();

        return new S2eBookProjection
        {
            BusinessId = businessId,
            FromInclusive = fromInclusive,
            ToExclusive = toExclusive,
            OpeningBalance = sections.Sum(x => x.OpeningBalance),
            TotalIn = sections.Sum(x => x.TotalIn),
            TotalOut = sections.Sum(x => x.TotalOut),
            EndingBalance = sections.Sum(x => x.EndingBalance),
            Accounts = sections,
            Blockers = blockers
        };
    }

    private static List<S2eValidationBlocker> ValidateMovements(
        IReadOnlyList<MoneyMovement> movements)
    {
        var blockers = new List<S2eValidationBlocker>();

        foreach (var movement in movements)
        {
            if (!MoneyMovementTypes.All.Contains(movement.MovementType))
            {
                blockers.Add(new S2eValidationBlocker
                {
                    Code = S2eValidationBlockerCodes.InvalidMovementType,
                    Message = "Sổ tiền có phát sinh mang loại không được hỗ trợ.",
                    PaymentAccountId = movement.PaymentAccountId,
                    ReferenceId = movement.ReferenceId
                });
            }

            if (movement.Amount <= 0)
            {
                blockers.Add(new S2eValidationBlocker
                {
                    Code = S2eValidationBlockerCodes.InvalidMovementAmount,
                    Message = "Sổ tiền có phát sinh với số tiền không lớn hơn 0.",
                    PaymentAccountId = movement.PaymentAccountId,
                    ReferenceId = movement.ReferenceId
                });
            }
        }

        foreach (var duplicate in movements
                     .GroupBy(x => new { x.MovementType, x.ReferenceId })
                     .Where(x => x.Count() > 1))
        {
            blockers.Add(new S2eValidationBlocker
            {
                Code = S2eValidationBlockerCodes.DuplicateMovementSource,
                Message = "Một nghiệp vụ nguồn đang được ghi nhận nhiều lần trên Sổ tiền.",
                ReferenceId = duplicate.Key.ReferenceId
            });
        }

        return blockers;
    }

    private static void ValidateSources(
        IReadOnlyList<MoneyMovement> movements,
        IEnumerable<MoneyMovementSourceAuditRecord> expectedSources,
        IReadOnlySet<Guid> systemIncomeIds,
        ICollection<S2eValidationBlocker> blockers)
    {
        var expectedSourceList = expectedSources.ToList();
        var movementsBySource = movements
            .GroupBy(x => (x.MovementType, x.ReferenceId))
            .ToDictionary(x => x.Key, x => x.First());
        var expectedBySource = expectedSourceList
            .GroupBy(x => (x.MovementType, x.ReferenceId))
            .ToDictionary(x => x.Key, x => x.First());

        foreach (var expected in expectedSourceList)
        {
            if (!movementsBySource.TryGetValue(
                    (expected.MovementType, expected.ReferenceId),
                    out var actual))
            {
                blockers.Add(new S2eValidationBlocker
                {
                    Code = S2eValidationBlockerCodes.MissingSourceMovement,
                    Message = "Nghiệp vụ đã thu hoặc chi tiền nhưng chưa có phát sinh tương ứng trên Sổ tiền.",
                    PaymentAccountId = expected.PaymentAccountId,
                    ReferenceId = expected.ReferenceId
                });
                continue;
            }

            var accountMismatch =
                expected.PaymentAccountId.HasValue &&
                actual.PaymentAccountId != expected.PaymentAccountId.Value;
            if (actual.Amount != expected.Amount ||
                MovementInstant(actual) != BangkokBusinessTime.NormalizeNaiveUtc(
                    expected.MovementDate) ||
                accountMismatch)
            {
                blockers.Add(new S2eValidationBlocker
                {
                    Code = S2eValidationBlockerCodes.SourceMovementMismatch,
                    Message = "Phát sinh trên Sổ tiền không khớp số tiền, ngày hoặc tài khoản của nghiệp vụ nguồn.",
                    PaymentAccountId = actual.PaymentAccountId,
                    ReferenceId = expected.ReferenceId
                });
            }
        }

        foreach (var movement in movements.Where(x =>
                     !expectedBySource.ContainsKey(
                         (x.MovementType, x.ReferenceId))))
        {
            blockers.Add(new S2eValidationBlocker
            {
                Code = S2eValidationBlockerCodes.OrphanMovementSource,
                Message = "Phát sinh trên Sổ tiền không còn nghiệp vụ nguồn tương ứng.",
                PaymentAccountId = movement.PaymentAccountId,
                ReferenceId = movement.ReferenceId
            });
        }

        foreach (var movement in movements.Where(x =>
                     x.MovementType == MoneyMovementTypes.ManualIncomeIn &&
                     systemIncomeIds.Contains(x.ReferenceId)))
        {
            blockers.Add(new S2eValidationBlocker
            {
                Code = S2eValidationBlockerCodes.AutoIncomeDuplicateMovement,
                Message = "Khoản doanh thu tự sinh từ đơn hàng không được tạo thêm dòng thu thủ công.",
                PaymentAccountId = movement.PaymentAccountId,
                ReferenceId = movement.ReferenceId
            });
        }
    }

    private static void ValidateAccount(
        PaymentAccount account,
        DateTime fromInclusive,
        ICollection<S2eValidationBlocker> blockers)
    {
        if (!PaymentAccountTypes.All.Contains(account.AccountType))
        {
            blockers.Add(new S2eValidationBlocker
            {
                Code = S2eValidationBlockerCodes.InvalidAccountType,
                Message = "Tài khoản tiền có loại không được hỗ trợ.",
                PaymentAccountId = account.PaymentAccountId
            });
        }

        if (account.AccountType == PaymentAccountTypes.Bank &&
            (string.IsNullOrWhiteSpace(account.BankShortName) ||
             string.IsNullOrWhiteSpace(account.BankName) ||
             string.IsNullOrWhiteSpace(account.AccountNumber) ||
             string.IsNullOrWhiteSpace(account.AccountName)))
        {
            blockers.Add(new S2eValidationBlocker
            {
                Code = S2eValidationBlockerCodes.InvalidBankAccount,
                Message = "Tài khoản ngân hàng thiếu thông tin bắt buộc để lập Sổ tiền.",
                PaymentAccountId = account.PaymentAccountId
            });
        }

        if (!account.InitialBalance.HasValue || !account.InitialBalanceDate.HasValue)
        {
            blockers.Add(new S2eValidationBlocker
            {
                Code = S2eValidationBlockerCodes.InitialBalanceUnconfirmed,
                Message = "Chưa xác nhận số dư ban đầu của tài khoản tiền.",
                PaymentAccountId = account.PaymentAccountId
            });
            return;
        }

        var initialDate = ToInitialBalanceInstant(
            account.InitialBalanceDate.Value);
        if (initialDate > fromInclusive)
        {
            blockers.Add(new S2eValidationBlocker
            {
                Code = S2eValidationBlockerCodes.InitialBalanceAfterPeriodStart,
                Message = "Ngày số dư ban đầu phải không muộn hơn ngày bắt đầu kỳ S2e.",
                PaymentAccountId = account.PaymentAccountId
            });
        }
    }

    private static S2eBalanceCalculation CalculateAccount(
        PaymentAccount account,
        DateTime fromInclusive,
        DateTime toExclusive,
        IReadOnlyList<MoneyMovement> movements)
    {
        if (!account.InitialBalance.HasValue || !account.InitialBalanceDate.HasValue)
        {
            return new S2eBalanceCalculation();
        }

        var initialDate = ToInitialBalanceInstant(
            account.InitialBalanceDate.Value);
        if (initialDate > fromInclusive)
        {
            return new S2eBalanceCalculation();
        }

        return S2eBalanceCalculator.Calculate(
            account.InitialBalance.Value,
            initialDate,
            fromInclusive,
            toExclusive,
            movements);
    }

    private static S2eBookEntry ToEntry(MoneyMovement movement)
    {
        var isInflow = S2eBalanceCalculator.IsInflow(movement.MovementType);
        return new S2eBookEntry
        {
            MoneyMovementId = movement.MoneyMovementId,
            MovementDate = MovementInstant(movement),
            DocumentNumber = movement.DocumentNumber,
            Description = movement.Description,
            AmountIn = isInflow ? movement.Amount : 0,
            AmountOut = isInflow ? 0 : movement.Amount,
            ReferenceId = movement.ReferenceId
        };
    }

    private static string GetDisplayName(PaymentAccount account)
    {
        if (account.AccountType == PaymentAccountTypes.Cash)
        {
            return "Tiền mặt";
        }

        var accountNumber = account.AccountNumber?.Trim() ?? string.Empty;
        var suffix = accountNumber.Length <= 4
            ? accountNumber
            : accountNumber[^4..];
        return string.IsNullOrEmpty(suffix)
            ? account.BankName?.Trim() ?? "Tài khoản ngân hàng"
            : $"{account.BankName?.Trim() ?? "Tài khoản ngân hàng"} ••••{suffix}";
    }

    private static bool IsAccountRelevant(
        PaymentAccount account,
        IReadOnlyCollection<MoneyMovement> movements,
        DateTime toExclusive)
    {
        var initialDate = account.InitialBalanceDate.HasValue
            ? ToInitialBalanceInstant(account.InitialBalanceDate.Value)
            : (DateTime?)null;
        var createdAt = account.CreatedAt == default
            ? (DateTime?)null
            : BangkokBusinessTime.NormalizeNaiveUtc(account.CreatedAt);
        var existedBeforePeriodEnd =
            account.AccountType == PaymentAccountTypes.Cash ||
            movements.Count > 0 ||
            initialDate < toExclusive ||
            (!initialDate.HasValue &&
             (!createdAt.HasValue || createdAt < toExclusive));

        return existedBeforePeriodEnd && (
            account.AccountType == PaymentAccountTypes.Cash ||
            account.IsActive ||
            movements.Count > 0 ||
            account.InitialBalance.GetValueOrDefault() != 0);
    }

    private static DateTime ToInitialBalanceInstant(DateOnly date)
        => BangkokBusinessTime.BangkokWallClockToNaiveUtc(
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));

    private static DateTime MovementInstant(MoneyMovement movement)
        => BangkokBusinessTime.NormalizeNaiveUtc(movement.MovementDate);
}
