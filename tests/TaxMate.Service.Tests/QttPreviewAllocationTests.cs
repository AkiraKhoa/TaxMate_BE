using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Moq;
using TaxMate.Infrastructure.Documents.Tax;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.DTO.Tax;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Interfaces.Documents;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class QttPreviewAllocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportPreview_OpenQ4_ExportsRefundAndOffsetsWithoutSaving(bool internalOffset)
    {
        var h = new Harness();
        var request = h.Request(internalOffset);
        var file = await h.Service.ExportPreviewAsync(h.OwnerId, h.BusinessId, 2026, allocation: request);
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.Content), false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        var tables = body.Elements<Table>().ToList();
        var indicators = tables[0].Elements<TableRow>().Skip(1).ToDictionary(
            r => r.Elements<TableCell>().ElementAt(2).InnerText.Trim(),
            r => r.Elements<TableCell>().Last().InnerText);
        Assert.Equal("50.902.425", indicators["[20]"]);
        Assert.Equal("15.000.000", indicators["[21]"]);
        Assert.Equal("10.000.000", indicators["[22]"]);
        Assert.Equal("5.000.000", indicators["[23]"]);
        Assert.Equal("35.902.425", indicators["[24]"]);
        Assert.Contains("K04 nhận hoàn", body.InnerText);
        Assert.Contains("9900040000", body.InnerText);
        Assert.Contains("Vietcombank", body.InnerText);
        Assert.Contains("GTGT-Q4", tables[3].InnerText);
        Assert.Equal("[44] 50.902.425", tables[2].Elements<TableRow>().Last().Elements<TableCell>().ElementAt(1).InnerText);
        Assert.Equal("15.000.000", tables[3].Elements<TableRow>().Last().Elements<TableCell>().Last().InnerText);
        Assert.False(h.Aggregate.CanClose);
        Assert.Contains("XEM-TRUOC", file.FileName);
        h.AssertReadOnly();
        h.Declarations.Verify(x => x.GetCurrentByTaxPeriodAndFormAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Declarations.Verify(x => x.GetCurrentCalculationWithLinesAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(60_000_000, true)]
    [InlineData(10_000_000, false)]
    public async Task ExportPreview_RejectsInvalidRefund(decimal amount, bool withAccount)
    {
        var h = new Harness();
        await Assert.ThrowsAsync<BadRequestException>(() => h.Service.ExportPreviewAsync(
            h.OwnerId, h.BusinessId, 2026, allocation: new()
            { RefundAmount = amount, RefundPaymentAccountId = withAccount ? h.AccountId : null }));
        h.AssertReadOnly();
    }

    [Fact]
    public async Task ExportPreview_RejectsPaidInternalObligation()
    {
        var h = new Harness();
        h.Obligation.TaxDeclaration.TaxPeriod.PaidDate = DateTime.UtcNow;
        await Assert.ThrowsAsync<NotFoundException>(() => h.Service.ExportPreviewAsync(
            h.OwnerId, h.BusinessId, 2026, allocation: h.Request(true)));
        h.AssertReadOnly();
    }

    [Fact]
    public async Task ExistingGetPreview_PreservesSavedDraftAllocation()
    {
        var h = new Harness();
        var calculated = new QttCalculationEngine().Calculate(h.Aggregate);
        var snapshot = new QttFormSnapshot
        {
            TaxYear = 2026, TaxpayerName = "K04", TaxCode = "0123456789",
            Indicators = calculated.Indicators with { Indicator21 = 10_000_000, Indicator22 = 10_000_000, Indicator24 = 40_902_425 },
            InventoryTotals = calculated.InventoryTotals,
            RefundAccount = new(h.AccountId, "K04 nhận hoàn", "9900040000", "Vietcombank")
        };
        h.Declarations.Setup(x => x.GetCurrentByTaxPeriodAndFormAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxDeclaration { Status = "Draft", FormDataJson = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        var file = await h.Service.ExportPreviewAsync(h.OwnerId, h.BusinessId, 2026);
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.Content), false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.Contains("K04 nhận hoàn", body.InnerText);
        Assert.Contains("40.902.425", body.InnerText);
        h.Annual.Verify(x => x.PreviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        h.AssertReadOnly();
    }

    [Fact]
    public async Task UpdateAllocation_StillSavesDraftAndIncrementsRevision()
    {
        var h = new Harness();
        var calculated = new QttCalculationEngine().Calculate(h.Aggregate);
        var declaration = new TaxDeclaration
        {
            Id = Guid.NewGuid(), Status = "Draft", FormCode = "02/CNKD-TNCN-QTT",
            TaxPeriod = new TaxPeriod { Business = new BusinessProfile { OwnerId = h.OwnerId } },
            FormDataJson = JsonSerializer.Serialize(new QttFormSnapshot
            {
                DraftRevision = 1, Indicators = calculated.Indicators, InventoryTotals = calculated.InventoryTotals
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
        h.Declarations.Setup(x => x.GetByIdAsync(declaration.Id, It.IsAny<CancellationToken>())).ReturnsAsync(declaration);
        h.Declarations.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var result = await h.Service.UpdateAllocationAsync(h.OwnerId, h.BusinessId, declaration.Id, new()
        {
            RefundAmount = 10_000_000, RefundPaymentAccountId = h.AccountId, ExpectedRevision = 1
        });
        Assert.Equal(2, result.DraftRevision);
        Assert.Equal(10_000_000, result.Indicators.Indicator22);
        Assert.Equal(40_902_425, result.Indicators.Indicator24);
        Assert.Equal("9900040000", result.RefundAccount!.AccountNumber);
        Assert.Contains("9900040000", declaration.FormDataJson);
        h.Declarations.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Harness
    {
        public Guid OwnerId { get; } = Guid.NewGuid();
        public Guid BusinessId { get; } = Guid.NewGuid();
        public Guid AccountId { get; } = Guid.NewGuid();
        public Mock<ITaxDeclarationRepository> Declarations { get; } = new(MockBehavior.Strict);
        public Mock<IAnnualTaxAggregateService> Annual { get; } = new(MockBehavior.Strict);
        public TaxDeclarationObligation Obligation { get; }
        public QttPreviewResponse Aggregate { get; }
        public QttDeclarationService Service { get; }

        public Harness()
        {
            var business = new BusinessProfile { Id = BusinessId, OwnerId = OwnerId, BusinessName = "K04", Address = "Demo" };
            var periods = new Mock<ITaxPeriodRepository>(MockBehavior.Strict);
            periods.Setup(x => x.BusinessBelongsToUserAsync(BusinessId, OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            periods.Setup(x => x.GetBusinessWithCategoryAsync(BusinessId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
            periods.Setup(x => x.GetYearAsync(BusinessId, 2026, It.IsAny<CancellationToken>())).ReturnsAsync(new TaxPeriod { Id = Guid.NewGuid(), Status = "Open" });
            var accounts = new Mock<IGenericRepository<PaymentAccount>>(MockBehavior.Strict);
            accounts.Setup(x => x.GetByIdAsync(AccountId)).ReturnsAsync(new PaymentAccount
            {
                PaymentAccountId = AccountId, BusinessId = BusinessId, IsActive = true,
                AccountType = "Bank", AccountName = "K04 nhận hoàn", AccountNumber = "9900040000", BankName = "Vietcombank"
            });
            Obligation = new TaxDeclarationObligation
            {
                Id = Guid.NewGuid(), PayableAmount = 20_000_000, StateBudgetContent = "Thuế GTGT",
                TaxDeclaration = new TaxDeclaration
                {
                    Status = "Submitted", IsCurrent = true, TaxCode = "0123456789", TaxpayerName = "K04", DeclarationCode = "GTGT-Q4",
                    TaxPeriod = new TaxPeriod { Business = business }
                }
            };
            Declarations.Setup(x => x.GetObligationsByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) => ids.Contains(Obligation.Id) ? [Obligation] : []);
            Aggregate = new QttPreviewResponse
            {
                OwnerId = OwnerId, TaxYear = 2026, Eligibility = "NormalIncomeBased",
                Revenue = new(4_804_585_000, 0, 0), Expenses = new(5_873_571_100, 0, 0, 0, 0, 0, 0, 0), Inventory = new(),
                HardBlockers = [new("Quarter4NotClosed", "Q4 còn mở")],
                PitPayments = new()
                {
                    Indicator15 = 50_902_425,
                    Payments = new[] { 15_773_175m, 17_114_700m, 18_014_550m }.Select((amount, i) =>
                        new QttPitPaymentLine(Guid.NewGuid(), $"Q{i + 1}", new DateTime(2026, 4 + i * 3, 20), amount, "PIT", "Completed", "IncomeBased", true)).ToList()
                }
            };
            Annual.Setup(x => x.PreviewAsync(OwnerId, BusinessId, 2026, It.IsAny<CancellationToken>())).ReturnsAsync(Aggregate);
            Service = new(periods.Object, Declarations.Object, accounts.Object, new OpenXmlQttDocumentGenerator(),
                new Mock<IUnitOfWork>(MockBehavior.Strict).Object, new Mock<IAccountingTransactionLockRepository>(MockBehavior.Strict).Object,
                Annual.Object, new QttCalculationEngine());
        }

        public QttPreviewAllocationRequest Request(bool internalOffset) => new()
        {
            RefundAmount = 10_000_000, OffsetAmount = 5_000_000, RefundPaymentAccountId = AccountId,
            OffsetItems = [new()
            {
                TaxDeclarationObligationId = internalOffset ? Obligation.Id : null,
                TaxCode = "0123456789", TaxpayerName = "K04", ObligationIdentifier = "GTGT-Q4", BudgetContent = "Thuế GTGT",
                OutstandingAmount = 20_000_000, OffsetAmount = 5_000_000
            }]
        };

        public void AssertReadOnly() => Declarations.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
