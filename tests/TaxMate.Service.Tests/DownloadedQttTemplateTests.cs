using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TaxMate.Infrastructure.Documents.Tax;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.DTO.Tax;

namespace TaxMate.Service.Tests;

public class DownloadedQttTemplateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_PreservesOfficialHeadersAndMapsMergedTotals(bool withRows)
    {
        var model = new QttDocumentModel
        {
            ExportDate = new DateTime(2026, 12, 31),
            Snapshot = new QttFormSnapshot
            {
                TaxYear = 2026, TaxpayerName = "Nguyễn Văn An", TaxCode = "012345678901",
                TaxpayerAddress = "18 Hoa Mai, phường Bến Thành, TP Hồ Chí Minh",
                Indicators = new(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 15, 14,
                    15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25),
                InventoryTotals = new(100, 200, 50, 250),
                InventoryRows = withRows
                    ? [new(Guid.Empty, null, null, "HH01", "Hàng hóa A", 100, 200, 50, 250)] : [],
                RefundAccount = new(Guid.Empty, "Nguyễn Văn An", "123456789", "Vietcombank"),
                OffsetItems = withRows
                    ? [new(null, "012345678901", "Nguyễn Văn An", "ID-01", "Thuế TNCN", "757",
                        "1003", "Cơ quan thu", "001", new DateTime(2027, 1, 31), 500, 200, 300)] : []
            },
            PaymentSupportRows = withRows
                ? [new("Thuế TNCN", 1200, "757", "1003", "001", "Cơ quan thu", "Cơ quan thuế", new DateTime(2027, 1, 31)),
                   new("Thuế TNCN", 3400, "757", "1003", "001", "Cơ quan thu", "Cơ quan thuế", new DateTime(2027, 1, 31))] : []
        };
        var result = await new OpenXmlQttDocumentGenerator().GenerateAsync(model);
        await File.WriteAllBytesAsync(Path.Combine(AppContext.BaseDirectory,
            withRows ? "qtt-template-qa.docx" : "qtt-empty-template-qa.docx"), result.Content);
        using var doc = WordprocessingDocument.Open(new MemoryStream(result.Content), false);
        var body = doc.MainDocumentPart!.Document.Body!;
        Assert.DoesNotContain("{{", body.InnerText);
        Assert.Contains("Nguyễn Văn An", body.InnerText);
        Assert.Contains("Vietcombank", body.InnerText);
        var tables = body.Elements<Table>().ToList();
        Assert.Equal(5, tables.Count);
        var indicatorRows = tables[0].Elements<TableRow>().Skip(1).ToDictionary(
            r => r.Elements<TableCell>().ElementAt(2).InnerText.Trim(),
            r => r.Elements<TableCell>().Last().InnerText);
        Assert.Equal("4", indicatorRows["[09c]"]);
        Assert.Equal("9", indicatorRows["[10d]"]);
        Assert.Equal("10", indicatorRows["[10đ]"]);
        Assert.Equal("15%", indicatorRows["[12]"]);
        Assert.Equal("25", indicatorRows["[24]"]);
        Assert.Contains("[31] 100", tables[1].InnerText);
        var paymentRows = tables[2].Elements<TableRow>().ToList();
        Assert.Equal(withRows ? 5 : 3, paymentRows.Count);
        var totalCells = paymentRows.Last().Elements<TableCell>().ToList();
        Assert.Equal(8, totalCells.Count);
        Assert.Equal(2, totalCells[0].TableCellProperties!.GridSpan!.Val!.Value);
        Assert.Equal(withRows ? "[44] 4.600" : "[44] 0", totalCells[1].InnerText);
        var offsetRows = tables[3].Elements<TableRow>().ToList();
        Assert.Equal(withRows ? 4 : 3, offsetRows.Count);
        Assert.Contains("[46]", offsetRows[2].InnerText);
        Assert.Contains("[58]", offsetRows[2].InnerText);
        if (withRows)
        {
            Assert.Equal("ID-01", offsetRows[3].Elements<TableCell>().ElementAt(3).InnerText);
            Assert.Equal("300", offsetRows[3].Elements<TableCell>().Last().InnerText);
        }
    }
}
