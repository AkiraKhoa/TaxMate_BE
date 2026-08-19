using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TaxMate.Infrastructure.Word;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Tests;

public class S2aHkdWordServiceTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    [Fact]
    public async Task GenerateDocxAsync_FillsOfficialTemplateWithBusinessAndTaxTotals()
    {
        var service = new S2aHkdWordService();
        var model = new S2aHkdDocumentModel
        {
            Header = new S2aHkdHeaderModel
            {
                BusinessName = "Cua Hang Test S2A",
                Address = "44a Vườn Lài",
                TaxCode = "12345566",
                DeclarationPeriod = "Quý I/2026",
                Unit = "Đồng"
            },
            Groups =
            [
                new S2aHkdCategoryGroupModel
                {
                    GroupNumber = 1,
                    CategoryName = "Bán tạp hóa",
                    VatRate = 1m,
                    PitRate = 0.5m,
                    Lines =
                    [
                        new S2aHkdLineModel
                        {
                            DocumentNumber = "TM001",
                            TransactionDate = new DateTime(2026, 1, 10),
                            Description = "Dầu ăn",
                            Amount = 200_000_000m
                        }
                    ],
                    Subtotal = 200_000_000m,
                    VatTax = 2_000_000m,
                    PitTax = 1_000_000m
                }
            ],
            Footer = new S2aHkdFooterModel
            {
                TotalVatTax = 2_000_000m,
                TotalPitTax = 1_000_000m,
                ExportDate = new DateTime(2026, 3, 31)
            }
        };

        var bytes = await service.GenerateDocxAsync([model]);

        Assert.NotEmpty(bytes);
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);

        var texts = ExtractAllText(bytes);
        Assert.Contains("Cua Hang Test S2A", texts);
        Assert.Contains("12345566", texts);
        Assert.Contains("Quý I/2026", texts);
        Assert.Contains("TM001", texts);
        Assert.Contains("Dầu ăn", texts);
        Assert.Contains("2.000.000", texts);
        Assert.Contains("1.000.000", texts);
        Assert.Contains("Tổng số thuế GTGT phải trả", texts);
        Assert.Contains("Tổng số thuế TNCN phải trả", texts);
        Assert.Contains("NGƯỜI ĐẠI DIỆN", texts);
    }

    [Fact]
    public async Task GenerateDocxAsync_CombinesMultipleBusinessesInOneFile()
    {
        var service = new S2aHkdWordService();
        var first = CreateSampleModel("Cua Hang A", 2_000_000m, 1_000_000m);
        var second = CreateSampleModel("Cua Hang B", 3_000_000m, 1_500_000m);

        var bytes = await service.GenerateDocxAsync([first, second]);
        var texts = ExtractAllText(bytes);

        Assert.Contains("Cua Hang A", texts);
        Assert.Contains("Cua Hang B", texts);
        Assert.Contains("Tổng số thuế GTGT phải trả", texts);
        Assert.Contains("2.000.000", texts);
        Assert.Contains("3.000.000", texts);
    }

    private static S2aHkdDocumentModel CreateSampleModel(string businessName, decimal vat, decimal pit)
    {
        return new S2aHkdDocumentModel
        {
            Header = new S2aHkdHeaderModel
            {
                BusinessName = businessName,
                Address = "44a Vườn Lài",
                TaxCode = "12345566",
                DeclarationPeriod = "Quý I/2026",
                Unit = "Đồng"
            },
            Groups =
            [
                new S2aHkdCategoryGroupModel
                {
                    GroupNumber = 1,
                    CategoryName = "Bán tạp hóa",
                    VatRate = 1m,
                    PitRate = 0.5m,
                    Lines =
                    [
                        new S2aHkdLineModel
                        {
                            DocumentNumber = "TM001",
                            TransactionDate = new DateTime(2026, 1, 10),
                            Description = "Dầu ăn",
                            Amount = vat * 100
                        }
                    ],
                    Subtotal = vat * 100,
                    VatTax = vat,
                    PitTax = pit
                }
            ],
            Footer = new S2aHkdFooterModel
            {
                TotalVatTax = vat,
                TotalPitTax = pit,
                ExportDate = new DateTime(2026, 3, 31)
            }
        };
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
