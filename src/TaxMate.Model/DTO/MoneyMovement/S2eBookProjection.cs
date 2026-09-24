namespace TaxMate.Model.DTO.MoneyMovement;

public static class S2eValidationBlockerCodes
{
    public const string InitialBalanceUnconfirmed = "InitialBalanceUnconfirmed";
    public const string InitialBalanceAfterPeriodStart = "InitialBalanceAfterPeriodStart";
    public const string InvalidAccountType = "InvalidAccountType";
    public const string InvalidBankAccount = "InvalidBankAccount";
    public const string InvalidMovementType = "InvalidMovementType";
    public const string InvalidMovementAmount = "InvalidMovementAmount";
    public const string DuplicateMovementSource = "DuplicateMovementSource";
    public const string MissingSourceMovement = "MissingSourceMovement";
    public const string SourceMovementMismatch = "SourceMovementMismatch";
    public const string OrphanMovementSource = "OrphanMovementSource";
    public const string AutoIncomeDuplicateMovement = "AutoIncomeDuplicateMovement";
}

public sealed class S2eValidationBlocker
{
    public string Code { get; init; } = null!;
    public string Message { get; init; } = null!;
    public Guid? PaymentAccountId { get; init; }
    public Guid? ReferenceId { get; init; }
}

public sealed class S2eBookEntry
{
    public Guid MoneyMovementId { get; init; }
    public DateTime MovementDate { get; init; }
    public string DocumentNumber { get; init; } = null!;
    public string Description { get; init; } = null!;
    public decimal AmountIn { get; init; }
    public decimal AmountOut { get; init; }
    public Guid ReferenceId { get; init; }
}

public sealed class S2eAccountSection
{
    public Guid PaymentAccountId { get; init; }
    public string AccountType { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public bool IsActive { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
    public decimal EndingBalance { get; init; }
    public IReadOnlyList<S2eBookEntry> Entries { get; init; } = [];
}

public sealed class S2eBookProjection
{
    public Guid BusinessId { get; init; }
    public DateTime FromInclusive { get; init; }
    public DateTime ToExclusive { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
    public decimal EndingBalance { get; init; }
    public IReadOnlyList<S2eAccountSection> Accounts { get; init; } = [];
    public IReadOnlyList<S2eValidationBlocker> Blockers { get; init; } = [];
    public bool IsReady => Blockers.Count == 0;
}
