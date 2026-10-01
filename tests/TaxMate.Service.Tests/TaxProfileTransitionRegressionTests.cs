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
    [Fact]
    public async Task ResolvedThreeBAlert_DoesNotInviteAnotherConfirmation()
    {
        var f = new Fixture(4_000_000_000m);
        f.ClockYear = 2026;
        f.Owner.PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.IncomeBased;
        f.Owner.TaxMethodEffectiveYear = 2026;
        f.Alerts.Add(new RevenueThresholdAlert {
            Id = Guid.NewGuid(), OwnerId = f.Owner.Id, Year = 2026,
            ThresholdCode = RevenueThresholdCodes.Crossed3B,
            ThresholdAmount = 3_000_000_000m, Status = RevenueThresholdAlertStatuses.Resolved
        });
        f.ReturnEvaluatedAlerts();
        var reviews = await f.ProfileService().GetThresholdReviewsAsync(f.Owner.Id, f.Business.Id, 2026);
        var review = Assert.Single(reviews);
        Assert.False(review.CanConfirm);
        Assert.Contains("đã được xử lý", review.Message);
        Assert.Equal(PersonalIncomeTaxMethods.IncomeBased, f.Owner.PersonalIncomeTaxMethod);
        Assert.Equal(2026, f.Owner.TaxMethodEffectiveYear);
    }

    [Theory]
    [InlineData(2026, false)]
    [InlineData(2025, true)]
    public async Task HistoricalConclusion_CannotClearLaterProfile(int effectiveYear, bool filedLater)
    {
        var f = new Fixture(0m);
        f.ClockYear = 2026;
        f.Owner.TaxMethodEffectiveYear = effectiveYear;
        if (filedLater) f.CompleteQuarters(PersonalIncomeTaxMethods.RevenueBased);
        var preview = await f.ProfileService().PreviewAnnualConclusionAsync(f.Owner.Id, f.Business.Id, 2025);
        Assert.False(preview.CanConfirm);
        Assert.Contains(preview.BlockingIssues, x => x.Code == "LaterTaxProfileInUse");
        await Assert.ThrowsAsync<ConflictException>(() => f.ProfileService().ConfirmAnnualConclusionAsync(
            f.Owner.Id, f.Business.Id, 2025, new ConfirmAnnualRevenueConclusionRequest(true, null)));
        Assert.Equal(PersonalIncomeTaxMethods.RevenueBased, f.Owner.PersonalIncomeTaxMethod);
        Assert.Equal(effectiveYear, f.Owner.TaxMethodEffectiveYear);
    }

    [Fact]
    public async Task GenuineZeroRevenue_CanStillConcludeWithoutLaterProfile()
    {
        var f = new Fixture(0m);
        f.CompleteQuarters(PersonalIncomeTaxMethods.RevenueBased);
        var result = await f.ProfileService().ConfirmAnnualConclusionAsync(f.Owner.Id, f.Business.Id, 2026,
            new ConfirmAnnualRevenueConclusionRequest(true, null));
        Assert.True(result.AlreadyConfirmed);
    }

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

    [Fact]
    public async Task CrossingQuarter_NewAccount_FourQuarters_CorrectPoolConsumption()
    {
        // Q1 = 600M, Q2 = 300M, Q3 = 500M, Q4 = 700M -> Annual = 2,100M (crosses 1B in Q3)
        var f = new Fixture(500_000_000m);
        f.Owner.PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased;
        f.Owner.TaxMethodEffectiveYear = 2026;
        f.Period.Year = 2026;
        f.SetFourQuartersCrossingInQuarter3();

        // Q1 & Q2 should throw ConflictException when calculating or previewing 01/CNKD
        f.Period.Quarter = 1;
        f.Period.PeriodStartDate = new DateTime(2025, 12, 31, 17, 0, 0);
        f.Period.PeriodEndDate = new DateTime(2026, 3, 31, 17, 0, 0);
        await Assert.ThrowsAsync<ConflictException>(() => f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id));
        await Assert.ThrowsAsync<ConflictException>(() => f.QuarterService().GetCalculationPreviewAsync(f.Owner.Id, f.Period.Id));

        f.Period.Quarter = 2;
        f.Period.PeriodStartDate = new DateTime(2026, 3, 31, 17, 0, 0);
        f.Period.PeriodEndDate = new DateTime(2026, 6, 30, 17, 0, 0);
        await Assert.ThrowsAsync<ConflictException>(() => f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id));
        await Assert.ThrowsAsync<ConflictException>(() => f.QuarterService().GetCalculationPreviewAsync(f.Owner.Id, f.Period.Id));

        // Q3 (Crossing quarter, revenue = 500M) -> gets full 1B pool, deduction = 500M, remaining = 500M, PIT = 0
        f.Period.Quarter = 3;
        f.Period.PeriodStartDate = new DateTime(2026, 6, 30, 17, 0, 0);
        f.Period.PeriodEndDate = new DateTime(2026, 9, 30, 17, 0, 0);

        var resultQ3 = await f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(500_000_000m, resultQ3.TotalRevenue);
        Assert.Equal(5_000_000m, resultQ3.TotalVatTaxAmount); // 500M * 1%
        Assert.Equal(0m, resultQ3.TotalPersonalIncomeTaxAmount); // 500M - 500M deducted = 0
        Assert.Equal(500_000_000m, resultQ3.RemainingPitDeduction); // 1B - 500M = 500M

        var previewQ3 = await f.QuarterService().GetCalculationPreviewAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(500_000_000m, previewQ3.TotalRevenue);
        Assert.Equal(5_000_000m, previewQ3.TotalVatTaxAmount);
        Assert.Equal(0m, previewQ3.TotalPersonalIncomeTaxAmount);
        Assert.Equal(500_000_000m, previewQ3.RemainingPitDeduction);

        // Q4 (Subsequent quarter, revenue = 700M) -> previous revenue in pool window (Q3) = 500M, remaining pool = 500M
        // Deducts 500M -> PIT base = 700M - 500M = 200M -> PIT = 200M * 0.5% = 1,000,000đ, remaining pool = 0
        f.Period.Quarter = 4;
        f.Period.PeriodStartDate = new DateTime(2026, 9, 30, 17, 0, 0);
        f.Period.PeriodEndDate = new DateTime(2026, 12, 31, 17, 0, 0);

        var resultQ4 = await f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(700_000_000m, resultQ4.TotalRevenue);
        Assert.Equal(7_000_000m, resultQ4.TotalVatTaxAmount); // 700M * 1%
        Assert.Equal(1_000_000m, resultQ4.TotalPersonalIncomeTaxAmount); // (700M - 500M) * 0.5%
        Assert.Equal(0m, resultQ4.RemainingPitDeduction);

        var previewQ4 = await f.QuarterService().GetCalculationPreviewAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(700_000_000m, previewQ4.TotalRevenue);
        Assert.Equal(7_000_000m, previewQ4.TotalVatTaxAmount);
        Assert.Equal(1_000_000m, previewQ4.TotalPersonalIncomeTaxAmount);
        Assert.Equal(0m, previewQ4.RemainingPitDeduction);
    }

    [Fact]
    public async Task CarriedMethod_OldAccount_FourQuarters_CorrectPoolConsumptionAndYearIndependence()
    {
        // Old account: elected in 2025, calculating 2026
        // Q1 = 600M, Q2 = 700M, Q3 = 500M, Q4 = 400M -> Annual 2,200M
        var f = new Fixture(600_000_000m);
        f.Owner.PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased;
        f.Owner.TaxMethodEffectiveYear = 2025; // Carried method from previous year
        f.Period.Year = 2026;
        f.SetFourQuartersCarriedMethod();

        // Q1: revenue = 600M -> pool 1B -> deduct 600M -> PIT = 0, remaining = 400M
        f.Period.Quarter = 1;
        f.Period.PeriodStartDate = new DateTime(2025, 12, 31, 17, 0, 0);
        f.Period.PeriodEndDate = new DateTime(2026, 3, 31, 17, 0, 0);

        var resultQ1 = await f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(600_000_000m, resultQ1.TotalRevenue);
        Assert.Equal(6_000_000m, resultQ1.TotalVatTaxAmount);
        Assert.Equal(0m, resultQ1.TotalPersonalIncomeTaxAmount);
        Assert.Equal(400_000_000m, resultQ1.RemainingPitDeduction);

        // Q2: revenue = 700M -> previous revenue (Q1) = 600M -> pool remaining = 400M
        // Deduct 400M -> PIT base = 300M -> PIT = 300M * 0.5% = 1.5M, remaining = 0
        f.Period.Quarter = 2;
        f.Period.PeriodStartDate = new DateTime(2026, 3, 31, 17, 0, 0);
        f.Period.PeriodEndDate = new DateTime(2026, 6, 30, 17, 0, 0);

        var resultQ2 = await f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(700_000_000m, resultQ2.TotalRevenue);
        Assert.Equal(7_000_000m, resultQ2.TotalVatTaxAmount);
        Assert.Equal(1_500_000m, resultQ2.TotalPersonalIncomeTaxAmount);
        Assert.Equal(0m, resultQ2.RemainingPitDeduction);

        // Q3: revenue = 500M -> previous revenue (Q1+Q2) = 1,300M -> pool remaining = 0
        // PIT base = 500M -> PIT = 500M * 0.5% = 2.5M, remaining = 0
        f.Period.Quarter = 3;
        f.Period.PeriodStartDate = new DateTime(2026, 6, 30, 17, 0, 0);
        f.Period.PeriodEndDate = new DateTime(2026, 9, 30, 17, 0, 0);

        var resultQ3 = await f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(500_000_000m, resultQ3.TotalRevenue);
        Assert.Equal(5_000_000m, resultQ3.TotalVatTaxAmount);
        Assert.Equal(2_500_000m, resultQ3.TotalPersonalIncomeTaxAmount);
        Assert.Equal(0m, resultQ3.RemainingPitDeduction);

        // Q4: revenue = 400M -> previous revenue (Q1+Q2+Q3) = 1,800M -> pool remaining = 0
        // PIT base = 400M -> PIT = 400M * 0.5% = 2.0M, remaining = 0
        f.Period.Quarter = 4;
        f.Period.PeriodStartDate = new DateTime(2026, 9, 30, 17, 0, 0);
        f.Period.PeriodEndDate = new DateTime(2026, 12, 31, 17, 0, 0);

        var resultQ4 = await f.QuarterService().CalculateAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(400_000_000m, resultQ4.TotalRevenue);
        Assert.Equal(4_000_000m, resultQ4.TotalVatTaxAmount);
        Assert.Equal(2_000_000m, resultQ4.TotalPersonalIncomeTaxAmount);
        Assert.Equal(0m, resultQ4.RemainingPitDeduction);

        var previewQ4 = await f.QuarterService().GetCalculationPreviewAsync(f.Owner.Id, f.Period.Id);
        Assert.Equal(400_000_000m, previewQ4.TotalRevenue);
        Assert.Equal(4_000_000m, previewQ4.TotalVatTaxAmount);
        Assert.Equal(2_000_000m, previewQ4.TotalPersonalIncomeTaxAmount);
        Assert.Equal(0m, previewQ4.RemainingPitDeduction);
    }

    private sealed class Fixture
    {
        public User Owner = new() { Id = Guid.NewGuid(), DeclaredRevenueBracket = RevenueBrackets.Over1BTo3B,
            PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.RevenueBased,
            TaxMethodEffectiveYear = 2025, TaxProfileConfirmedAt = new DateTime(2025, 1, 1) };
        public BusinessProfile Business;
        public TaxPeriod Period;
        public List<RevenueThresholdAlert> Alerts = [];
        public List<OwnerRevenueLine> AnnualLines = [];
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
                .ReturnsAsync((Guid oId, Guid bId, DateTime start, DateTime end, CancellationToken ct) =>
                {
                    if (AnnualLines.Count > 0)
                    {
                        var matchingLines = AnnualLines.Where(l => l.DocumentDate >= start && l.DocumentDate < end).ToList();
                        var rev = matchingLines.Sum(l => l.Amount);
                        return new OwnerRevenueProjection(Owner.Id, start, end, rev, 0m, [])
                        {
                            Groups = rev > 0
                                ? [new OwnerRevenueGroup(Business.MainCategory!.BusinessCategoryId, "DIST_GOODS", "Goods", 1m, rev, 0m)]
                                : []
                        };
                    }
                    return new OwnerRevenueProjection(Owner.Id, start, end, total, 0m, [])
                    {
                        Groups = [new OwnerRevenueGroup(Business.MainCategory!.BusinessCategoryId, "DIST_GOODS", "Goods", 1m, total, 0m)]
                    };
                });
            policy.Setup(x => x.GetEffectiveAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync(new EffectiveTaxPolicyResponse {
                AnnualRevenueThreshold = 1_000_000_000m, IncomeBasedRequirementThreshold = 3_000_000_000m, SupportedRevenueCeiling = 50_000_000_000m });
            alerts.Setup(x => x.FindAsync(It.IsAny<Expression<Func<RevenueThresholdAlert,bool>>>()))
                .ReturnsAsync((Expression<Func<RevenueThresholdAlert,bool>> predicate) => Alerts.Where(predicate.Compile()).ToList());
            alerts.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => Alerts.FirstOrDefault(x => x.Id == id));
            evaluator.Setup(x => x.EvaluateAsync(Owner.Id, Business.Id, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
            periods.Setup(x => x.GetOwnerTaxMethodHistoryAsync(Owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        }
        public void ReturnEvaluatedAlerts() => evaluator.Setup(x => x.EvaluateAsync(
            Owner.Id, Business.Id, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Alerts);
        public void SetFourQuartersCrossingInQuarter3()
        {
            AnnualLines =
            [
                new OwnerRevenueLine(Business.MainCategory!.BusinessCategoryId, "DIST_GOODS", Guid.NewGuid(), "Order", "Q1", new DateTime(2026, 2, 10, 5, 0, 0), "Q1", 600_000_000m),
                new OwnerRevenueLine(Business.MainCategory!.BusinessCategoryId, "DIST_GOODS", Guid.NewGuid(), "Order", "Q2", new DateTime(2026, 5, 10, 5, 0, 0), "Q2", 300_000_000m),
                new OwnerRevenueLine(Business.MainCategory!.BusinessCategoryId, "DIST_GOODS", Guid.NewGuid(), "Order", "Q3", new DateTime(2026, 8, 10, 5, 0, 0), "Q3", 500_000_000m),
                new OwnerRevenueLine(Business.MainCategory!.BusinessCategoryId, "DIST_GOODS", Guid.NewGuid(), "Order", "Q4", new DateTime(2026, 11, 10, 5, 0, 0), "Q4", 700_000_000m)
            ];

            revenue.Setup(x => x.ProjectCalendarYearAsync(Owner.Id, Business.Id, 2026, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OwnerRevenueProjection(Owner.Id, new DateTime(2025, 12, 31, 17, 0, 0), new DateTime(2026, 12, 31, 17, 0, 0), 2_100_000_000m, 0m, [])
                {
                    Lines = AnnualLines
                });
        }

        public void SetFourQuartersCarriedMethod()
        {
            AnnualLines =
            [
                new OwnerRevenueLine(Business.MainCategory!.BusinessCategoryId, "DIST_GOODS", Guid.NewGuid(), "Order", "Q1", new DateTime(2026, 2, 10, 5, 0, 0), "Q1", 600_000_000m),
                new OwnerRevenueLine(Business.MainCategory!.BusinessCategoryId, "DIST_GOODS", Guid.NewGuid(), "Order", "Q2", new DateTime(2026, 5, 10, 5, 0, 0), "Q2", 700_000_000m),
                new OwnerRevenueLine(Business.MainCategory!.BusinessCategoryId, "DIST_GOODS", Guid.NewGuid(), "Order", "Q3", new DateTime(2026, 8, 10, 5, 0, 0), "Q3", 500_000_000m),
                new OwnerRevenueLine(Business.MainCategory!.BusinessCategoryId, "DIST_GOODS", Guid.NewGuid(), "Order", "Q4", new DateTime(2026, 11, 10, 5, 0, 0), "Q4", 400_000_000m)
            ];

            revenue.Setup(x => x.ProjectCalendarYearAsync(Owner.Id, Business.Id, 2026, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OwnerRevenueProjection(Owner.Id, new DateTime(2025, 12, 31, 17, 0, 0), new DateTime(2026, 12, 31, 17, 0, 0), 2_200_000_000m, 0m, [])
                {
                    Lines = AnnualLines
                });
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
