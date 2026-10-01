using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class TaxPeriodPaymentServiceTests
{
    [Fact]
    public async Task RecordPayment_CreatesCompletedTaxPaymentsAndSetsStatusToPaid()
    {
        var ownerId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var (start, end) = BangkokBusinessTime.GetQuarterNaiveUtc(2026, 1);
        var period = new TaxPeriod
        {
            Id = periodId,
            BusinessId = businessId,
            PeriodType = TaxPeriodTypes.Quarterly,
            Year = 2026,
            Quarter = 1,
            PeriodStartDate = start,
            PeriodEndDate = end,
            VatTaxAmount = 2_000_000m,
            PersonalIncomeTaxAmount = 1_500_000m,
            Status = TaxPeriodStatuses.Submitted
        };

        var declaration = new TaxDeclaration
        {
            Id = Guid.NewGuid(),
            TaxPeriodId = periodId,
            FormCode = "01/CNKD",
            IsCurrent = true,
            Obligations = new List<TaxDeclarationObligation>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TaxType = TaxTypes.Vat,
                    PayableAmount = 2_000_000m,
                    StateBudgetChapterCode = "857",
                    StateBudgetSubsectionCode = "1701"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TaxType = TaxTypes.PersonalIncomeTax,
                    PayableAmount = 1_500_000m,
                    StateBudgetChapterCode = "857",
                    StateBudgetSubsectionCode = "1003"
                }
            }
        };

        var periods = new Mock<ITaxPeriodRepository>();
        periods.Setup(x => x.GetIdentityAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPeriodIdentity(periodId, businessId, ownerId, 2026));
        periods.Setup(x => x.GetCanonicalByIdAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        periods.Setup(x => x.GetPaymentsByPeriodIdAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        List<TaxPayment> savedPayments = [];
        periods.Setup(x => x.AddTaxPaymentsAsync(It.IsAny<IEnumerable<TaxPayment>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<TaxPayment>, CancellationToken>((p, _) => savedPayments.AddRange(p))
            .Returns(Task.CompletedTask);

        var declarations = new Mock<ITaxDeclarationRepository>();
        declarations.Setup(x => x.GetCurrentByTaxPeriodAsync(periodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(declaration);

        var calcs = new Mock<ITaxCalculationRepository>();
        var policies = new Mock<ITaxPolicyService>();
        var uow = new Mock<IUnitOfWork>();
        var locks = new Mock<IAccountingTransactionLockRepository>();
        var s2e = new Mock<IS2eBookProjector>();
        var inventory = new Mock<IInventoryMovementRepository>();
        var valuation = new Mock<IInventoryQuarterFinalizer>();

        var service = new TaxPeriodService(
            periods.Object,
            calcs.Object,
            policies.Object,
            uow.Object,
            locks.Object,
            s2e.Object,
            inventory.Object,
            valuation.Object,
            taxDeclarationRepository: declarations.Object);

        var paymentDate = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var request = new RecordTaxPeriodPaymentRequest(
            PaymentDate: paymentDate,
            PaymentMethod: "Bank",
            TransactionReference: "TXN-20260420-9999",
            ReceiptFileUrl: "https://example.com/receipt.jpg",
            Note: "Nộp thuế Q1/2026");

        var response = await service.RecordPaymentAsync(ownerId, periodId, request);

        Assert.Equal(TaxPeriodStatuses.Paid, response.PeriodStatus);
        Assert.Equal(paymentDate, response.PaidDate);
        Assert.Equal(3_500_000m, response.TotalPaidAmount);
        Assert.Equal(2, response.Payments.Count);

        Assert.Equal(2, savedPayments.Count);
        var pitPayment = savedPayments.First(x => x.TaxType == TaxTypes.PersonalIncomeTax);
        Assert.Equal(1_500_000m, pitPayment.Amount);
        Assert.Equal(TaxPaymentStatuses.Completed, pitPayment.Status);
        Assert.Equal("TXN-20260420-9999", pitPayment.TransactionReference);

        var vatPayment = savedPayments.First(x => x.TaxType == TaxTypes.Vat);
        Assert.Equal(2_000_000m, vatPayment.Amount);
        Assert.Equal(TaxPaymentStatuses.Completed, vatPayment.Status);
    }
}
