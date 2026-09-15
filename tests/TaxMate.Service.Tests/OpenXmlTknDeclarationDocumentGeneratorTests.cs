using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TaxMate.Infrastructure.Documents.Tax;
using TaxMate.Model.Documents.Tax;

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
