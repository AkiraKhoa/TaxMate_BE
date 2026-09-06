using System.Linq.Expressions;
using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.TaxProfile;
using TaxMate.Model.DTO.TaxPolicy;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;
using TaxMate.Service.Exceptions;

namespace TaxMate.Service.Tests;

public class TaxProfileTransitionRegressionTests
{
    [Theory]
    [InlineData(PersonalIncomeTaxMethods.RevenueBased)]
    [InlineData(PersonalIncomeTaxMethods.IncomeBased)]
    public async Task CarriedMethodUnderThreshold_CanCalculateThenCompleteAnnualReview(string method)
    {
        var f = new Fixture(900_000_000m);
        f.Owner.PersonalIncomeTaxMethod = method;
        var result = await f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(method, result.TaxMethod);
        Assert.Equal(900_000_000m, result.TotalRevenue);
        Assert.Equal(9_000_000m, result.TotalVatTaxAmount);
        Assert.Equal(method == PersonalIncomeTaxMethods.IncomeBased ? 4_500_000m : 0m,
            result.TotalPersonalIncomeTaxAmount);
        var preview = await f.ProfileService().PreviewAnnualConclusionAsync(f.Owner.Id, f.Business.Id, 2026);
        Assert.False(preview.CanConfirm);
        Assert.Equal(4, preview.Quarters.Count);
        Assert.Contains(preview.BlockingIssues, x => x.Code == "Quarter1NotCompleted");
        f.CompleteQuarters(method);
        var confirmed = await f.ProfileService().ConfirmAnnualConclusionAsync(f.Owner.Id, f.Business.Id, 2026,
            new ConfirmAnnualRevenueConclusionRequest(true, null));
        Assert.True(confirmed.AlreadyConfirmed);
        Assert.Null(f.Owner.PersonalIncomeTaxMethod);
        Assert.Equal(RevenueBrackets.AtOrBelow1B, f.Owner.DeclaredRevenueBracket);
        var task = Assert.Single(await f.ScheduleService().GetTasksAsync(f.Owner.Id, f.Business.Id, 2026));
        Assert.True(task.Eligibility.IsEligible);
    }

    [Theory]
    [InlineData(PersonalIncomeTaxMethods.RevenueBased)]
    [InlineData(PersonalIncomeTaxMethods.IncomeBased)]
    public async Task OldBelowThresholdAcknowledgement_DoesNotBlockNewYear(string method)
    {
        var f = new Fixture(2_000_000_000m);
        f.Owner.PersonalIncomeTaxMethod = method;
        f.Period.Year = 2027;
        f.Alerts.Add(new RevenueThresholdAlert {
            Id = Guid.NewGuid(), OwnerId = f.Owner.Id, Year = 2026,
            ThresholdCode = RevenueThresholdCodes.Crossed3B,
            ThresholdAmount = 3_000_000_000m, TotalRevenue = 2_000_000_000m,
            Status = RevenueThresholdAlertStatuses.Acknowledged
        });
        var result = await f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(method, result.TaxMethod);
        var profile = await f.ProfileService().GetCurrentAsync(f.Owner.Id, f.Business.Id);
        Assert.Empty(profile.ThresholdReviews);
    }

    [Theory]
    [InlineData(PersonalIncomeTaxMethods.RevenueBased, 2025, 2025)]
    [InlineData(PersonalIncomeTaxMethods.IncomeBased, 2025, 2025)]
    [InlineData(PersonalIncomeTaxMethods.IncomeBased, 2024, 2024)]
    public async Task ConfirmOneBAlert_PreservesExistingMethodAndEffectiveYear(string method, int effectiveYear, int observedYear)
    {
        var f = new Fixture(4_000_000_000m);
        f.Owner.PersonalIncomeTaxMethod = method;
        f.Owner.TaxMethodEffectiveYear = effectiveYear;
        f.ClockYear = 2026;
        var alert = new RevenueThresholdAlert {
            Id = Guid.NewGuid(), OwnerId = f.Owner.Id, Year = 2026,
            ThresholdCode = RevenueThresholdCodes.Crossed1B,
            ThresholdAmount = 1_000_000_000m, Status = RevenueThresholdAlertStatuses.PendingReview
        };
        f.Alerts.Add(alert);
        await f.ProfileService().ConfirmThresholdReviewAsync(f.Owner.Id, f.Business.Id, alert.Id,
            new ConfirmRevenueThresholdReviewRequest { Confirmed = true });
        Assert.Equal(method, f.Owner.PersonalIncomeTaxMethod);
        Assert.Equal(observedYear, f.Owner.TaxMethodEffectiveYear);

        // Compare the 3B action for the same initial profile and revenue.
        var comparison = new Fixture(4_000_000_000m);
        comparison.ClockYear = 2026;
        comparison.Owner.PersonalIncomeTaxMethod = method;
        comparison.Owner.TaxMethodEffectiveYear = effectiveYear;
        var otherAlert = new RevenueThresholdAlert {
            Id = Guid.NewGuid(), OwnerId = comparison.Owner.Id, Year = 2026,
            ThresholdCode = RevenueThresholdCodes.Crossed3B,
            ThresholdAmount = 3_000_000_000m, Status = RevenueThresholdAlertStatuses.PendingReview
        };
        comparison.Alerts.Add(otherAlert);
        await comparison.ProfileService().ConfirmThresholdReviewAsync(comparison.Owner.Id, comparison.Business.Id, otherAlert.Id,
            new ConfirmRevenueThresholdReviewRequest { Confirmed = true });
        Assert.Equal(method, comparison.Owner.PersonalIncomeTaxMethod);
        Assert.Equal(effectiveYear, comparison.Owner.TaxMethodEffectiveYear);

        // Applying both alerts in either order must converge.
        var followup = new RevenueThresholdAlert { Id = Guid.NewGuid(), OwnerId = f.Owner.Id, Year = 2026,
            ThresholdCode = RevenueThresholdCodes.Crossed3B, ThresholdAmount = 3_000_000_000m,
            Status = RevenueThresholdAlertStatuses.PendingReview };
        f.Alerts.Add(followup);
        await f.ProfileService().ConfirmThresholdReviewAsync(f.Owner.Id, f.Business.Id, followup.Id,
            new ConfirmRevenueThresholdReviewRequest { Confirmed = true });
        var earlier = new RevenueThresholdAlert { Id = Guid.NewGuid(), OwnerId = comparison.Owner.Id, Year = 2026,
            ThresholdCode = RevenueThresholdCodes.Crossed1B, ThresholdAmount = 1_000_000_000m,
            Status = RevenueThresholdAlertStatuses.PendingReview };
        comparison.Alerts.Add(earlier);
        await comparison.ProfileService().ConfirmThresholdReviewAsync(comparison.Owner.Id, comparison.Business.Id, earlier.Id,
            new ConfirmRevenueThresholdReviewRequest { Confirmed = true });
        Assert.Equal(f.Owner.PersonalIncomeTaxMethod, comparison.Owner.PersonalIncomeTaxMethod);
        Assert.Equal(f.Owner.TaxMethodEffectiveYear, comparison.Owner.TaxMethodEffectiveYear);
        Assert.Equal(f.Owner.DeclaredRevenueBracket, comparison.Owner.DeclaredRevenueBracket);
        await Assert.ThrowsAsync<ConflictException>(() => f.ProfileService().ConfirmThresholdReviewAsync(
            f.Owner.Id, f.Business.Id, alert.Id, new ConfirmRevenueThresholdReviewRequest { Confirmed = true }));
        Assert.Equal(observedYear, f.Owner.TaxMethodEffectiveYear);
    }

    [Theory]
    [InlineData(PersonalIncomeTaxMethods.RevenueBased)]
    [InlineData(PersonalIncomeTaxMethods.IncomeBased)]
    public async Task NewMethodUnderThreshold_StillCannotCalculateQuarter(string method)
    {
        var f = new Fixture(900_000_000m);
        f.Owner.PersonalIncomeTaxMethod = method;
        f.Owner.TaxMethodEffectiveYear = 2026;
        await Assert.ThrowsAsync<ConflictException>(() => f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id));
    }

    [Fact]
    public async Task ActualDeferredCrossing_BlocksRevenueBasedButNotIncomeBased()
    {
        var f = new Fixture(4_000_000_000m);
        f.Period.Year = 2027;
        var alert = new RevenueThresholdAlert { Id = Guid.NewGuid(), OwnerId = f.Owner.Id, Year = 2026,
            ThresholdCode = RevenueThresholdCodes.Crossed3B, ThresholdAmount = 3_000_000_000m,
            Status = RevenueThresholdAlertStatuses.Acknowledged };
        f.Alerts.Add(alert);
        await Assert.ThrowsAsync<ConflictException>(() => f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id));
        var profile = await f.ProfileService().GetCurrentAsync(f.Owner.Id, f.Business.Id);
        Assert.True(Assert.Single(profile.ThresholdReviews).CanConfirm);
        await f.ProfileService().ConfirmThresholdReviewAsync(f.Owner.Id, f.Business.Id, alert.Id,
            new ConfirmRevenueThresholdReviewRequest { Confirmed = true });
        Assert.Equal(2027, f.Owner.TaxMethodEffectiveYear);
        Assert.Equal(PersonalIncomeTaxMethods.IncomeBased,
            (await f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id)).TaxMethod);
    }

    [Fact]
    public async Task NewOwnerOverThreeB_StillStartsIncomeBased()
    {
        var f = new Fixture(4_000_000_000m);
        f.ClockYear = 2026;
        f.Owner.PersonalIncomeTaxMethod = null;
        f.Owner.TaxMethodEffectiveYear = null;
        var alert = new RevenueThresholdAlert { Id = Guid.NewGuid(), OwnerId = f.Owner.Id, Year = 2026,
            ThresholdCode = RevenueThresholdCodes.Crossed1B, ThresholdAmount = 1_000_000_000m,
            Status = RevenueThresholdAlertStatuses.PendingReview };
        f.Alerts.Add(alert);
        await f.ProfileService().ConfirmThresholdReviewAsync(f.Owner.Id, f.Business.Id, alert.Id,
            new ConfirmRevenueThresholdReviewRequest { Confirmed = true });
        Assert.Equal(PersonalIncomeTaxMethods.IncomeBased, f.Owner.PersonalIncomeTaxMethod);
        Assert.Equal(2026, f.Owner.TaxMethodEffectiveYear);
    }

    private sealed class Fixture
    {
        public User Owner = new() { Id = Guid.NewGuid(), DeclaredRevenueBracket = RevenueBrackets.Over1BTo3B,
            PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased,
            TaxMethodEffectiveYear = 2025, TaxProfileConfirmedAt = new DateTime(2025, 1, 1) };
        public BusinessProfile Business;
        public TaxPeriod Period;
        public List<RevenueThresholdAlert> Alerts = [];
        public int ClockYear = 2027;
        private readonly Mock<ITaxPeriodRepository> periods = new();
        private readonly Mock<IUserRepository> users = new();
        private readonly Mock<IOwnerRevenueProjector> revenue = new();
        private readonly Mock<ITaxPolicyService> policy = new();
        private readonly Mock<IGenericRepository<RevenueThresholdAlert>> alerts = new();
        private readonly Mock<IRevenueThresholdAlertService> evaluator = new();
        private readonly Mock<IUnitOfWork> uow = new();

        public Fixture(decimal total)
        {
            Business = new() { Id = Guid.NewGuid(), OwnerId = Owner.Id, Owner = Owner, IsActive = true };
            Business.MainCategory = new BusinessCategory { BusinessCategoryId = Guid.NewGuid(), Code = "DIST_GOODS",
                Name = "Goods", VatRate = 1m, PitRate = 0.5m };
            Period = new() { Id = Guid.NewGuid(), BusinessId = Business.Id, Year = 2026, Quarter = 1,
                PeriodType = TaxPeriodTypes.Quarterly, Status = TaxPeriodStatuses.Closed,
                PeriodStartDate = new DateTime(2025,12,31,17,0,0), PeriodEndDate = new DateTime(2026,3,31,17,0,0) };
            users.Setup(x => x.GetByIdAsync(Owner.Id)).ReturnsAsync(Owner);
            periods.Setup(x => x.GetByIdAsync(Period.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Period);
            periods.Setup(x => x.BusinessBelongsToUserAsync(Business.Id, Owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            periods.Setup(x => x.GetBusinessWithCategoryAsync(Business.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Business);
            periods.Setup(x => x.GetBusinessesWithCategoriesByOwnerAsync(Owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync([Business]);
            periods.Setup(x => x.GetOwnerQuarterlyFilingStatesAsync(Owner.Id, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
            revenue.Setup(x => x.ProjectCalendarYearAsync(Owner.Id, Business.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OwnerRevenueProjection(Owner.Id, new DateTime(2025,12,31,17,0,0), new DateTime(2026,12,31,17,0,0), total, 0m, []));
            revenue.Setup(x => x.ProjectAsync(Owner.Id, Business.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OwnerRevenueProjection(Owner.Id, Period.PeriodStartDate, Period.PeriodEndDate, total, 0m, []) {
                    Groups = [new OwnerRevenueGroup(Business.MainCategory.BusinessCategoryId, "DIST_GOODS", "Goods", 1m, total, 0m)] });
            policy.Setup(x => x.GetEffectiveAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync(new EffectiveTaxPolicyResponse {
                AnnualRevenueThreshold = 1_000_000_000m, IncomeBasedRequirementThreshold = 3_000_000_000m, SupportedRevenueCeiling = 50_000_000_000m });
            alerts.Setup(x => x.FindAsync(It.IsAny<Expression<Func<RevenueThresholdAlert,bool>>>()))
                .ReturnsAsync((Expression<Func<RevenueThresholdAlert,bool>> predicate) => Alerts.Where(predicate.Compile()).ToList());
            alerts.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => Alerts.FirstOrDefault(x => x.Id == id));
            evaluator.Setup(x => x.EvaluateAsync(Owner.Id, Business.Id, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
            periods.Setup(x => x.GetOwnerTaxMethodHistoryAsync(Owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        }
        public void CompleteQuarters(string method) => periods.Setup(x => x.GetOwnerQuarterlyFilingStatesAsync(
                Owner.Id, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 4).Select(q => new OwnerQuarterlyFilingState(Guid.NewGuid(), q,
                TaxPeriodStatuses.Submitted, method == PersonalIncomeTaxMethods.IncomeBased,
                method == PersonalIncomeTaxMethods.RevenueBased, true)).ToList());
        public TaxPeriodService QuarterService() => new(periods.Object, Mock.Of<ITaxCalculationRepository>(), policy.Object,
            uow.Object, Mock.Of<IAccountingTransactionLockRepository>(), Mock.Of<IS2eBookProjector>(),
            Mock.Of<IInventoryMovementRepository>(), Mock.Of<IInventoryQuarterFinalizer>(), revenue.Object, alerts.Object);
        public OwnerTaxProfileService ProfileService() => new(users.Object, periods.Object, revenue.Object,
            policy.Object, evaluator.Object, alerts.Object, uow.Object, new FixedClock(ClockYear));
        public TaxFilingScheduleService ScheduleService() => new(periods.Object, policy.Object, revenue.Object, uow.Object);
    }
    private sealed class FixedClock(int year) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(year, 2, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
