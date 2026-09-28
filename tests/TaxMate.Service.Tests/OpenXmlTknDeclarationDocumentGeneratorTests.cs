using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Moq;
using TaxMate.Infrastructure.Documents.Tax;
using TaxMate.Model.Common;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Interfaces.Documents;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class OpenXmlTknDeclarationDocumentGeneratorTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    [Fact]
    public async Task GenerateAsync_ReplacesAllPlaceholdersAndFillsData()
    {
        var generator = new OpenXmlTknDeclarationDocumentGenerator();
        var snapshot = new Form01TknCnkd2026Snapshot
        {
            SchemaVersion = 1,
            FormCode = "01/TKN-CNKD",
            Year = 2026,
            PeriodSelector = "Year",
            DeclarationType = "Initial",
            IsAtOrBelowOneBillion = true,
            TaxpayerName = "Hộ Kinh Doanh Nguyễn Văn A",
            TaxCode = "0123456789-001",
            AuthorizedDeclarerName = "Đại lý khai hộ B",
            AuthorizedDeclarerTaxCode = "9876543210",
            TaxAgentName = "Công ty TNHH Thuế C",
            TaxAgentTaxCode = "1122334455",
            TaxAgentContractNumber = "HD-999",
            TaxAgentContractDate = new DateTime(2026, 1, 15),
            GeneratedAt = new DateTime(2026, 12, 31),
            WindowStart = new DateTime(2026, 1, 1),
            WindowEnd = new DateTime(2026, 12, 31),
            SectionALines =
            [
                new Form01TknCnkd2026LineSnapshot(
                    SectionCode: "A",
                    IndicatorCode: "08",
                    BusinessActivityCode: "ACT01",
                    BusinessActivityName: "Bán hàng hóa",
                    BusinessLocationId: Guid.NewGuid(),
                    BusinessLocationCode: "LOC01",
                    TotalRevenue: 842_970_000m,
                    VatNonTaxableRevenue: 842_970_000m,
                    ZeroRatedVatRevenue: 0m,
                    VatTaxAmount: 0m,
                    PersonalIncomeTaxableRevenue: 0m,
                    PersonalIncomeTaxDeductibleRevenue: 0m,
                    PersonalIncomeTaxAmount: 0m,
                    DisplayOrder: 1)
            ]
        };

        var result = await generator.GenerateAsync(snapshot);
        await File.WriteAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "tkn-template-qa.docx"), result.Content);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);

        var docxText = ExtractAllText(result.Content);

        // Verify that NO unreplaced template placeholders remain anywhere in the document
        Assert.DoesNotContain("{{", docxText);

        // Verify that actual values are correctly populated in paragraphs and tables
        Assert.Contains("Hộ Kinh Doanh Nguyễn Văn A", docxText);
        Assert.Contains("0123456789-001", docxText);
        Assert.Contains("☒ Hộ kinh doanh, cá nhân kinh doanh có doanh thu năm từ 01 tỷ đồng trở xuống", docxText);
        Assert.Contains("☐ Hộ kinh doanh, cá nhân kinh doanh mới ra kinh doanh có doanh thu năm từ 01 tỷ đồng trở xuống", docxText);
        Assert.Contains("[01a] Năm ☒ 2026", docxText);
        Assert.Contains("[01b] 6 tháng đầu năm ☐ 2026", docxText);
        Assert.Contains("[01c] 6 tháng cuối năm ☐ 2026", docxText);
        Assert.Contains("[02] Lần đầu: ☒", docxText);
        Assert.Contains("[04] Người nộp thuế: Hộ Kinh Doanh Nguyễn Văn A", docxText);
        Assert.Contains("[05] Mã số thuế: 0123456789-001", docxText);
        Assert.Contains("Đại lý khai hộ B", docxText);
        Assert.Contains("9876543210", docxText);
        Assert.Contains("Công ty TNHH Thuế C", docxText);
        Assert.Contains("1122334455", docxText);
        Assert.Contains("HD-999", docxText);
        Assert.Contains("15/01/2026", docxText);
        Assert.Contains("842.970.000", docxText);
        Assert.Contains("31 tháng 12 năm 2026", docxText);
    }

    [Fact]
    public async Task ExportPreviewAsync_WhenTknPeriodIsOpenAndUncalculated_ProjectsRevenueIntoDocxTable()
    {
        var userId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var taxPeriodId = Guid.NewGuid();
        var startDate = new DateTime(2026, 1, 1);
        var endDate = new DateTime(2026, 12, 31);

        var taxPeriod = new TaxPeriod
        {
            Id = taxPeriodId,
            BusinessId = businessId,
            Year = 2026,
            PeriodType = TaxPeriodTypes.Tkn,
            FilingWindow = TknFilingWindows.Annual,
            Status = TaxPeriodStatuses.Open,
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            DueDate = new DateTime(2027, 1, 31),
            TotalRevenue = 0m
        };

        var business = new BusinessProfile
        {
            Id = businessId,
            OwnerId = userId,
            BusinessName = "Bếp nhà An",
            Address = "123 Lê Lợi, Quận 1, TP.HCM",
            BusinessLocationCode = "LOC-01",
            Owner = new User
            {
                Id = userId,
                FullName = "Nguyễn Minh An",
                TaxCode = "012345678901"
            }
        };

        var taxPeriodRepo = new Mock<ITaxPeriodRepository>();
        taxPeriodRepo.Setup(x => x.GetByIdAsync(taxPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(taxPeriod);
        taxPeriodRepo.Setup(x => x.BusinessBelongsToUserAsync(businessId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        taxPeriodRepo.Setup(x => x.GetBusinessWithCategoryAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(business);
        taxPeriodRepo.Setup(x => x.GetBusinessesWithCategoriesByOwnerAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([business]);

        var taxDeclarationRepo = new Mock<ITaxDeclarationRepository>();
        taxDeclarationRepo.Setup(x => x.GetCurrentByTaxPeriodAsync(taxPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxDeclaration?)null);
        taxDeclarationRepo.Setup(x => x.GetCurrentCalculationWithLinesAsync(taxPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxCalculation?)null);

        var ownerRevenue = new Mock<IOwnerRevenueProjector>();
        ownerRevenue.Setup(x => x.ProjectAsync(userId, businessId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OwnerRevenueProjection(
                OwnerId: userId,
                StartNaiveUtc: startDate,
                EndExclusiveNaiveUtc: endDate.AddDays(1),
                CompletedTransactionRevenue: 842_970_000m,
                ManualBusinessRevenue: 24_000_000m,
                Blockers: [])
            {
                Groups =
                [
                    new OwnerRevenueGroup(
                        BusinessCategoryId: Guid.NewGuid(),
                        BusinessCategoryCode: "ACT01",
                        BusinessCategoryName: "Dịch vụ ăn uống",
                        VatRate: 3m,
                        CompletedTransactionRevenue: 842_970_000m,
                        ManualBusinessRevenue: 24_000_000m)
                ]
            });

        var docGenerator = new Mock<ITaxDeclarationDocumentGenerator>();
        var tknGenerator = new OpenXmlTknDeclarationDocumentGenerator();

        var service = new TaxDeclarationService(
            taxPeriodRepo.Object,
            taxDeclarationRepo.Object,
            docGenerator.Object,
            tknGenerator,
            ownerRevenue.Object);

        var exported = await service.ExportPreviewAsync(userId, taxPeriodId);

        Assert.NotNull(exported);
        Assert.Equal("01-TKN-CNKD_XEM-TRUOC_2026.docx", exported.FileName);
        Assert.NotEmpty(exported.Content);

        var docxText = ExtractAllText(exported.Content);

        Assert.DoesNotContain("{{", docxText);
        Assert.Contains("Nguyễn Minh An", docxText);
        Assert.Contains("012345678901", docxText);
        Assert.Contains("866.970.000", docxText);
    }

    private static string ExtractAllText(byte[] docxBytes)
    {
        using var zip = new ZipArchive(new MemoryStream(docxBytes), ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml")
            ?? throw new InvalidOperationException("document.xml missing from DOCX.");

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        var sb = new StringBuilder();
        foreach (var node in doc.Descendants(W + "t"))
            sb.Append(node.Value);
        return sb.ToString();
    }
}
