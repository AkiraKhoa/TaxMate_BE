using TaxMate.Model.Common;
using TaxMate.Model.Entities;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class InventoryValuationServiceTests
{
    private readonly FakeAccountingTransactionLockRepository _transactionLock = new()
    {
        HasActiveTransaction = true,
        CurrentTransactionId = CurrentTestTransactionId
    };
    private readonly InventoryValuationService _service;

    public InventoryValuationServiceTests()
    {
        _service = new InventoryValuationService(_transactionLock);
    }

    [Fact]
    public void Preview_UsesExactBangkokQuarterAndDoesNotMutateOutbound()
    {
        var productId = Guid.NewGuid();
        var (start, end) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var outbound = Movement(productId, InventoryMovementTypes.OrderOut, 5m, null, start.AddDays(10));
        var result = _service.PreviewQuarter(
            [
                Movement(productId, InventoryMovementTypes.OpeningBalance, 10m, 1_000_000m, start.AddTicks(-1)),
                Movement(productId, InventoryMovementTypes.PurchaseIn, 10m, 2_000_000m, start.AddDays(40)),
                outbound
            ],
            2026,
            1);

        var item = Assert.Single(result.Items);
        Assert.Equal(start, result.PeriodStart);
        Assert.Equal(end, result.PeriodEndExclusive);
        Assert.Equal(150_000m, item.WholePeriodAverageUnitValue);
        Assert.Equal(750_000m, item.OutboundValue);
        Assert.Equal(2_250_000m, item.EndingValue);
        Assert.Null(outbound.TotalValue);
    }

    [Fact]
    public void FinalizePeriod_AcceptsOnlyExactBangkokQuarterBoundaries()
    {
        var (quarterStart, quarterEnd) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var exact = _service.StageFinalizeBookPeriod([], quarterStart, quarterEnd);
        Assert.False(exact.IsProvisional);

        var (yearStart, yearEnd) = BangkokBusinessTime.GetCalendarYearNaiveUtc(2026);
        Assert.Throws<ArgumentException>(() =>
            _service.StageFinalizeBookPeriod([], yearStart, yearEnd));
        Assert.Throws<ArgumentException>(() =>
            _service.StageFinalizeBookPeriod(
                [],
                quarterStart.AddDays(1),
                quarterEnd));
        Assert.Throws<ArgumentException>(() =>
            _service.StageFinalizeBookPeriod(
                [],
                DateTime.SpecifyKind(quarterStart, DateTimeKind.Utc),
                quarterEnd));
    }

    [Fact]
    public void Finalize_ValuesAllOutboundWithOneQuarterAverage()
    {
        var productId = Guid.NewGuid();
        var (start, _) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var orderOut = Movement(productId, InventoryMovementTypes.OrderOut, 3m, null, start.AddDays(10));
        var adjustmentOut = Movement(productId, InventoryMovementTypes.AdjustmentOut, 2m, null, start.AddDays(80));
        var result = _service.StageFinalizeQuarter(
            [
                Movement(productId, InventoryMovementTypes.OpeningBalance, 10m, 1_000_000m, start.AddTicks(-1)),
                orderOut,
                Movement(productId, InventoryMovementTypes.PurchaseIn, 10m, 2_000_000m, start.AddDays(60)),
                adjustmentOut
            ],
            2026,
            1);

        Assert.True(result.CanFinalize);
        Assert.Equal(450_000m, orderOut.TotalValue);
        Assert.Equal(300_000m, adjustmentOut.TotalValue);
    }

    [Fact]
    public void NextQuarter_CarriesFinalizedClosingAsOpening()
    {
        var productId = Guid.NewGuid();
        var (q1Start, q1End) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var q1Out = Movement(productId, InventoryMovementTypes.OrderOut, 5m, null, q1Start.AddDays(10));
        var q2Out = Movement(productId, InventoryMovementTypes.OrderOut, 5m, null, q1End.AddDays(10));
        var movements = new[]
        {
            Movement(productId, InventoryMovementTypes.OpeningBalance, 10m, 1_000_000m, q1Start.AddTicks(-1)),
            Movement(productId, InventoryMovementTypes.PurchaseIn, 10m, 2_000_000m, q1Start.AddDays(40)),
            q1Out,
            q2Out
        };
        _service.StageFinalizeQuarter(
            movements.Where(x => x.OccurredAt < q1End).ToArray(),
            2026,
            1);

        var q2 = _service.PreviewQuarter(movements, 2026, 2);

        var item = Assert.Single(q2.Items);
        Assert.Equal(15m, item.OpeningQuantity);
        Assert.Equal(2_250_000m, item.OpeningValue);
        Assert.Equal(150_000m, item.WholePeriodAverageUnitValue);
    }

    [Fact]
    public void AdjustmentInWithoutValue_BlocksFinalization()
    {
        var productId = Guid.NewGuid();
        var (start, _) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var outbound = Movement(productId, InventoryMovementTypes.AdjustmentOut, 1m, null, start.AddDays(20));
        var result = _service.StageFinalizeQuarter(
            [
                Movement(productId, InventoryMovementTypes.OpeningBalance, 2m, 200m, start.AddTicks(-1)),
                Movement(productId, InventoryMovementTypes.AdjustmentIn, 1m, null, start.AddDays(10)),
                outbound
            ],
            2026,
            1);

        Assert.False(result.CanFinalize);
        Assert.Contains(result.Blockers, x => x.Code == InventoryBookBlockerCodes.MissingInboundValue);
        Assert.Null(outbound.TotalValue);
    }

    [Fact]
    public void ExhaustedInventory_AllocatesCentResidualDeterministicallyAndEndsAtZero()
    {
        var productId = Guid.NewGuid();
        var (start, _) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var first = Movement(productId, InventoryMovementTypes.OrderOut, 1m, null, start.AddDays(1));
        var second = Movement(productId, InventoryMovementTypes.OrderOut, 1m, null, start.AddDays(2));
        var third = Movement(productId, InventoryMovementTypes.OrderOut, 1m, null, start.AddDays(3));

        var result = _service.StageFinalizeQuarter(
            [
                Movement(productId, InventoryMovementTypes.OpeningBalance, 3m, 1m, start.AddTicks(-1)),
                third,
                first,
                second
            ],
            2026,
            1);

        Assert.Equal(0.34m, first.TotalValue);
        Assert.Equal(0.33m, second.TotalValue);
        Assert.Equal(0.33m, third.TotalValue);
        var item = Assert.Single(result.Items);
        Assert.Equal(1m, item.OutboundValue);
        Assert.Equal(0m, item.EndingQuantity);
        Assert.Equal(0m, item.EndingValue);
    }

    [Fact]
    public void PartialIssue_AllocatesResidualButPreservesRoundedClosingValue()
    {
        var productId = Guid.NewGuid();
        var (start, _) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var first = Movement(productId, InventoryMovementTypes.OrderOut, 1m, null, start.AddDays(1));
        var second = Movement(productId, InventoryMovementTypes.OrderOut, 1m, null, start.AddDays(2));

        var result = _service.StageFinalizeQuarter(
            [
                Movement(productId, InventoryMovementTypes.OpeningBalance, 3m, 1m, start.AddTicks(-1)),
                second,
                first
            ],
            2026,
            1);

        Assert.Equal(0.34m, first.TotalValue);
        Assert.Equal(0.33m, second.TotalValue);
        var item = Assert.Single(result.Items);
        Assert.Equal(0.67m, item.OutboundValue);
        Assert.Equal(1m, item.EndingQuantity);
        Assert.Equal(0.33m, item.EndingValue);
    }

    [Fact]
    public void Finalize_RejectsConflictingStoredValueWithoutPartialOverwrite()
    {
        var productId = Guid.NewGuid();
        var (start, _) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var conflict = Movement(productId, InventoryMovementTypes.OrderOut, 1m, 99m, start.AddDays(1));
        var untouched = Movement(productId, InventoryMovementTypes.OrderOut, 1m, null, start.AddDays(2));

        var result = _service.StageFinalizeQuarter(
            [
                Movement(productId, InventoryMovementTypes.OpeningBalance, 2m, 20m, start.AddTicks(-1)),
                conflict,
                untouched
            ],
            2026,
            1);

        Assert.False(result.CanFinalize);
        Assert.True(result.IsProvisional);
        Assert.Equal(99m, conflict.TotalValue);
        Assert.Null(untouched.TotalValue);
        Assert.Contains(
            result.Blockers,
            x => x.Code == InventoryBookBlockerCodes.ConflictingFinalizedOutboundValue);
    }

    [Fact]
    public void AnnualAggregate_RejectsEvidenceForAnotherYear()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.AggregateFinalizedCalendarYearOutboundValue(
                [],
                2026,
                Evidence(2025, TestBusinessId)));
    }

    [Fact]
    public void AnnualAggregate_FiltersMovementsToTrustedOwnerBusinessScope()
    {
        var productId = Guid.NewGuid();
        var siblingBusinessId = Guid.NewGuid();
        var outsideBusinessId = Guid.NewGuid();
        var (start, _) = BangkokBusinessTime.GetCalendarYearNaiveUtc(2026);
        var sibling = Movement(productId, InventoryMovementTypes.OrderOut, 1m, 50m, start.AddDays(1));
        sibling.BusinessId = siblingBusinessId;
        var outside = Movement(productId, InventoryMovementTypes.OrderOut, 1m, 900m, start.AddDays(1));
        outside.BusinessId = outsideBusinessId;

        var total = _service.AggregateFinalizedCalendarYearOutboundValue(
            [sibling, outside],
            2026,
            Evidence(2026, TestBusinessId, siblingBusinessId));

        Assert.Equal(50m, total);
    }

    [Fact]
    public void AnnualAggregate_SumsStoredValuesWithoutMutatingOrIncludingNextYear()
    {
        var productId = Guid.NewGuid();
        var (yearStart, yearEnd) = BangkokBusinessTime.GetCalendarYearNaiveUtc(2026);
        var q1 = Movement(productId, InventoryMovementTypes.OrderOut, 2m, 200m, yearStart.AddDays(20));
        var q2 = Movement(productId, InventoryMovementTypes.AdjustmentOut, 1m, 120m, yearStart.AddDays(120));
        var nextYear = Movement(productId, InventoryMovementTypes.OrderOut, 1m, 999m, yearEnd);

        var total = _service.AggregateFinalizedCalendarYearOutboundValue(
            [q1, q2, nextYear],
            2026,
            Evidence(2026, TestBusinessId));

        Assert.Equal(320m, total);
        Assert.Equal(200m, q1.TotalValue);
        Assert.Equal(120m, q2.TotalValue);
    }

    [Fact]
    public void AnnualAggregate_RejectsUnvaluedOutboundDespiteClosedQuarterEvidence()
    {
        var productId = Guid.NewGuid();
        var (start, _) = BangkokBusinessTime.GetCalendarYearNaiveUtc(2026);
        var exception = Assert.Throws<UnprocessableEntityException>(() =>
            _service.AggregateFinalizedCalendarYearOutboundValue(
                [Movement(productId, InventoryMovementTypes.OrderOut, 1m, null, start.AddDays(1))],
                2026,
                Evidence(2026, TestBusinessId)));

        Assert.Equal(InventoryBookBlockerCodes.MissingOutboundValue, exception.ErrorCode);
    }

    [Fact]
    public void AnnualAggregate_RejectsEvidenceAfterIssuingTransactionEnds()
    {
        var evidence = Evidence(2026, TestBusinessId);
        var endedTransaction = new FakeAccountingTransactionLockRepository
        {
            HasActiveTransaction = false,
            CurrentTransactionId = null
        };

        Assert.Throws<InvalidOperationException>(() =>
            new InventoryValuationService(endedTransaction)
                .AggregateFinalizedCalendarYearOutboundValue([], 2026, evidence));
    }

    private static InventoryMovement Movement(
        Guid productId,
        string type,
        decimal quantity,
        decimal? totalValue,
        DateTime occurredAt) => new()
    {
        InventoryMovementId = Guid.NewGuid(),
        BusinessId = TestBusinessId,
        ProductId = productId,
        MovementType = type,
        Quantity = quantity,
        TotalValue = totalValue,
        OccurredAt = occurredAt,
        DocumentNumber = "DOC",
        Description = "Description",
        ReferenceId = type is InventoryMovementTypes.PurchaseIn or InventoryMovementTypes.OrderOut
            ? Guid.NewGuid()
            : null
    };

    private static readonly Guid TestBusinessId = Guid.NewGuid();

    private static InventoryAnnualClosureEvidence Evidence(
        int year,
        params Guid[] businessIds) => new(
        Guid.NewGuid(),
        businessIds[0],
        year,
        businessIds,
        CurrentTestTransactionId);

    private static readonly Guid CurrentTestTransactionId = Guid.NewGuid();
}
