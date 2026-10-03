using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.DTO.TaxPeriod;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Interfaces.Documents;
using TaxMate.Service.Services;
using Xunit;

namespace TaxMate.Service.Tests;

public class TaxDeclarationPreviewTests
{
    [Theory]
    [InlineData(2025, 75_000_000, 1_875_000, 0)]
    [InlineData(2026, 200_000_000, 0, 700_000_000)]
    public async Task OpenQuarter_ExportsLiveLocationsAndDeductionWithoutSavedCalculation(
        int effectiveYear, decimal fnbDeduction, decimal pit, decimal remainingPool)
    {
        var f = new Fixture();
        f.Owner.TaxMethodEffectiveYear = effectiveYear;
        f.Preview.TotalPersonalIncomeTaxAmount = pit;
        f.Preview.TotalTaxPayableAmount = 11_000_000 + pit;
        f.Preview.RemainingPitDeduction = remainingPool;
        var fnb = f.Preview.Lines.Single(x => x.BusinessActivityCode == "FNB");
        fnb.PersonalIncomeTaxDeductibleRevenue = fnbDeduction;
        fnb.PersonalIncomeTaxRevenue = 200_000_000 - fnbDeduction;
        fnb.PersonalIncomeTaxAmount = pit;

        await f.Service.ExportPreviewAsync(f.Owner.Id, f.Period.Id);

        var model = Assert.IsType<Form01Cnkd2026Model>(f.Exported);
        Assert.Equal(300_000_000m, model.Summary.TotalRevenue);
        Assert.Equal(11_000_000m, model.Summary.TotalVatTaxAmount);
        Assert.Equal(pit, model.Summary.TotalPersonalIncomeTaxAmount);
        Assert.Equal(remainingPool, model.RemainingPitDeduction);
        Assert.Equal(2, model.Lines.Count);
        Assert.Equal("SERVICE-01", model.Lines[0].BusinessLocationCode);
        Assert.Equal("FNB-02", model.Lines[1].BusinessLocationCode);
        Assert.Equal("b", model.Lines[0].ActivityCode);
        Assert.Equal("d", model.Lines[1].ActivityCode);
        Assert.Equal(100_000_000m, model.Lines[0].PersonalIncomeTaxDeductibleRevenue);
        Assert.Equal(fnbDeduction, model.Lines[1].PersonalIncomeTaxDeductibleRevenue);
        Assert.Equal("Bếp demo", model.Lines[1].BusinessLocationName);
        Assert.Equal(TaxPeriodStatuses.Open, f.Period.Status);
        f.PreviewService.Verify(x => x.GetCalculationPreviewAsync(f.Owner.Id, f.Period.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        f.Periods.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.Declarations.Verify(x => x.GetCurrentCalculationWithLinesAsync(f.Period.Id,
            It.IsAny<CancellationToken>()), Times.Never);
        f.Declarations.Verify();
        f.Declarations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IneligiblePeriod_PropagatesPreviewRuleInsteadOfExportingBlankWord()
    {
        var f = new Fixture();
        f.PreviewService.Setup(x => x.GetCalculationPreviewAsync(f.Owner.Id, f.Period.Id,
            It.IsAny<CancellationToken>())).ThrowsAsync(new ConflictException("Use TKN."));
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ExportPreviewAsync(f.Owner.Id, f.Period.Id));
        Assert.Null(f.Exported);
    }

    private sealed class Fixture
    {
        public User Owner { get; } = new() { Id = Guid.NewGuid(), TaxCode = "012345678901" };
        public TaxPeriod Period { get; }
        public TaxCalculationResponse Preview { get; }
        public Mock<ITaxPeriodRepository> Periods { get; } = new();
        public Mock<ITaxDeclarationRepository> Declarations { get; } = new(MockBehavior.Strict);
        public Mock<ITaxPeriodService> PreviewService { get; } = new(MockBehavior.Strict);
        public TaxDeclarationService Service { get; }
        public Form01Cnkd2026Model? Exported { get; private set; }

        public Fixture()
        {
            var serviceBusiness = new BusinessProfile { Id = Guid.NewGuid(), OwnerId = Owner.Id,
                Owner = Owner, BusinessName = "Dịch vụ demo", BusinessLocationCode = "SERVICE-01" };
            var fnbBusiness = new BusinessProfile { Id = Guid.NewGuid(), OwnerId = Owner.Id,
                Owner = Owner, BusinessName = "Bếp demo", BusinessLocationCode = "FNB-02" };
            Period = new TaxPeriod { Id = Guid.NewGuid(), BusinessId = serviceBusiness.Id, Year = 2026,
                Quarter = 4, PeriodType = TaxPeriodTypes.Quarterly, Status = TaxPeriodStatuses.Open };
            Preview = new TaxCalculationResponse { TotalRevenue = 300_000_000, TotalVatTaxAmount = 11_000_000,
                Lines = [
                    new() { BusinessLocationId = fnbBusiness.Id, BusinessLocationCode = "FNB-02",
                        BusinessActivityCode = "FNB", BusinessActivityName = "FNB", IndicatorCode = "d",
                        SectionCode = "I", TotalRevenue = 200_000_000, VatTaxAmount = 6_000_000,
                        PersonalIncomeTaxableRevenue = 200_000_000 },
                    new() { BusinessLocationId = serviceBusiness.Id, BusinessLocationCode = "SERVICE-01",
                        BusinessActivityCode = "SERVICE", BusinessActivityName = "Dịch vụ", IndicatorCode = "b",
                        SectionCode = "I", TotalRevenue = 100_000_000, VatTaxAmount = 5_000_000,
                        PersonalIncomeTaxableRevenue = 100_000_000, PersonalIncomeTaxDeductibleRevenue = 100_000_000 }
                ] };
            Periods.Setup(x => x.GetByIdAsync(Period.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Period);
            Periods.Setup(x => x.BusinessBelongsToUserAsync(serviceBusiness.Id, Owner.Id,
                It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Periods.Setup(x => x.GetBusinessWithCategoryAsync(serviceBusiness.Id,
                It.IsAny<CancellationToken>())).ReturnsAsync(serviceBusiness);
            Periods.Setup(x => x.GetBusinessesWithCategoriesByOwnerAsync(Owner.Id,
                It.IsAny<CancellationToken>())).ReturnsAsync([serviceBusiness, fnbBusiness]);
            Declarations.Setup(x => x.GetCurrentByTaxPeriodAsync(Period.Id,
                It.IsAny<CancellationToken>())).ReturnsAsync((TaxDeclaration?)null).Verifiable();
            PreviewService.Setup(x => x.GetCalculationPreviewAsync(Owner.Id, Period.Id,
                It.IsAny<CancellationToken>())).ReturnsAsync(Preview);
            var generator = new Mock<ITaxDeclarationDocumentGenerator>();
            generator.Setup(x => x.GenerateAsync(It.IsAny<Form01Cnkd2026Model>(), It.IsAny<CancellationToken>()))
                .Callback<Form01Cnkd2026Model, CancellationToken>((model, _) => Exported = model)
                .ReturnsAsync(new TaxDeclarationGeneratedFile { Content = [1], FileName = "preview.docx",
                    ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document" });
            Service = new TaxDeclarationService(Periods.Object, Declarations.Object, generator.Object,
                Mock.Of<ITknDeclarationDocumentGenerator>(), taxPeriodService: PreviewService.Object);
        }
    }
}
