using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TaxMate.Infrastructure.Documents.Tax;
using TaxMate.Model.Documents.Tax;

namespace TaxMate.Service.Tests;

public class OpenXmlS2cDocumentGeneratorTests
{
    [Theory]
    [InlineData(15, 65_957_610, "65.957.610")]
    [InlineData(0, 0, "0")]
    public async Task GenerateAsync_WritesRateAndAmountIntoRow4(
        int rate, int amount, string expectedAmount)
    {
        var model = new S2cDocumentModel
        {
            BusinessName = "Cửa hàng A",
            BusinessLocation = "LOC-1",
            TaxCode = "0123456789",
            RepresentativeName = "Nguyễn Văn A",
            Year = 2026,
            Quarter = 4,
            ExportDate = new DateTime(2026, 10, 3),
            Revenue = 705_525_000m,
            MaterialCost = 237_007_600m,
            PurchasedServicesCost = 28_800_000m,
            PitRate = rate,
            PitAmount = amount
        };

        var file = await new OpenXmlS2cDocumentGenerator().GenerateAsync(model);

        using var stream = new MemoryStream(file.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart!.Document.Body!;
        var ledger = body.Elements<Table>().Last();
        var rows = ledger.Elements<TableRow>().ToList();
        Assert.Equal(13, rows.Count);
        var incomeCells = rows[11].Elements<TableCell>().ToList();
        Assert.Equal("439.717.400", incomeCells[3].InnerText);
        var taxCells = rows[12].Elements<TableCell>().ToList();
        Assert.Equal(4, taxCells.Count);
        Assert.Contains($"(3) x {rate}%", taxCells[2].InnerText);
        Assert.Equal(expectedAmount, taxCells[3].InnerText);
        Assert.Contains("Kỳ kê khai: Quý 4/2026", body.InnerText);
        Assert.Equal("S2c-HKD_0123456789_Q4_2026.docx", file.FileName);
    }
}
