using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure.Word;

public class S2aHkdWordService : IS2aHkdWordService
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public Task<byte[]> GenerateDocxAsync(S2aHkdDocumentModel model)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            AddParagraph(body, "Mẫu số S2a-HKD", bold: true, alignment: JustificationValues.Right);
            AddParagraph(
                body,
                "(Kèm theo Thông tư số 152/2025/TT-BTC ngày 31/12/2025 của Bộ trưởng Bộ Tài chính)",
                alignment: JustificationValues.Right);
            AddParagraph(body, string.Empty);

            AddParagraph(body, "SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ", bold: true, alignment: JustificationValues.Center);
            AddParagraph(body, string.Empty);

            AddParagraph(body, $"HỘ, CÁ NHÂN KINH DOANH: {model.Header.BusinessName}");
            AddParagraph(body, $"Địa chỉ: {model.Header.Address}");
            AddParagraph(body, $"Địa điểm kinh doanh: {model.Header.Address}");
            AddParagraph(body, $"Mã số thuế: {model.Header.TaxCode}");
            AddParagraph(body, $"Kỳ kê khai: {model.Header.DeclarationPeriod}");
            AddParagraph(body, $"Đơn vị tính: {model.Header.Unit}");
            AddParagraph(body, string.Empty);

            var table = new Table(
                new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4 },
                        new LeftBorder { Val = BorderValues.Single, Size = 4 },
                        new BottomBorder { Val = BorderValues.Single, Size = 4 },
                        new RightBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

            AddHeaderRow(table);
            foreach (var group in model.Groups)
            {
                AddGroupRows(table, group);
            }

            AddFooterRows(table, model.Footer);
            body.Append(table);

            AddParagraph(body, string.Empty);
            AddParagraph(
                body,
                $"Ngày {model.Footer.ExportDate.Day} tháng {model.Footer.ExportDate.Month} năm {model.Footer.ExportDate.Year}",
                alignment: JustificationValues.Right);

            mainPart.Document.Save();
        }

        return Task.FromResult(stream.ToArray());
    }

    private static void AddHeaderRow(Table table)
    {
        var row = new TableRow();
        row.Append(
            CreateCell("A\nSố hiệu", bold: true, widthPct: 12),
            CreateCell("B\nNgày, tháng", bold: true, widthPct: 15),
            CreateCell("C\nDiễn giải", bold: true, widthPct: 48),
            CreateCell("1\nSố tiền", bold: true, widthPct: 25, alignment: JustificationValues.Right));
        table.Append(row);
    }

    private static void AddGroupRows(Table table, S2aHkdCategoryGroupModel group)
    {
        var sectionRow = new TableRow();
        sectionRow.Append(
            CreateCell(string.Empty),
            CreateCell(string.Empty),
            CreateCell($"{group.GroupNumber}. Ngành nghề: {group.CategoryName}", bold: true),
            CreateCell(string.Empty));
        table.Append(sectionRow);

        foreach (var line in group.Lines)
        {
            var detailRow = new TableRow();
            detailRow.Append(
                CreateCell(line.DocumentNumber),
                CreateCell(line.TransactionDate.ToString("dd/MM/yyyy", ViCulture)),
                CreateCell(line.Description),
                CreateCell(FormatAmount(line.Amount), alignment: JustificationValues.Right));
            table.Append(detailRow);
        }

        AddSummaryRow(table, $"Tổng cộng ({group.GroupNumber})", group.Subtotal, bold: true);
        AddSummaryRow(
            table,
            $"Thuế GTGT ({FormatRate(group.VatRate)}%)",
            group.VatTax);
        AddSummaryRow(
            table,
            $"Thuế TNCN ({FormatRate(group.PitRate)}%)",
            group.PitTax);
    }

    private static void AddFooterRows(Table table, S2aHkdFooterModel footer)
    {
        AddSummaryRow(table, "Tổng số thuế GTGT phải nộp", footer.TotalVatTax, bold: true);
        AddSummaryRow(table, "Tổng số thuế TNCN phải nộp", footer.TotalPitTax, bold: true);
    }

    private static void AddSummaryRow(Table table, string label, decimal amount, bool bold = false)
    {
        var row = new TableRow();
        row.Append(
            CreateCell(string.Empty),
            CreateCell(string.Empty),
            CreateCell(label, bold: bold),
            CreateCell(FormatAmount(amount), bold: bold, alignment: JustificationValues.Right));
        table.Append(row);
    }

    private static TableCell CreateCell(
        string text,
        bool bold = false,
        int widthPct = 0,
        JustificationValues? alignment = null)
    {
        var paragraph = new Paragraph();
        if (alignment.HasValue)
        {
            paragraph.Append(new ParagraphProperties(new Justification { Val = alignment.Value }));
        }

        var run = new Run();
        if (bold)
        {
            run.Append(new RunProperties(new Bold()));
        }

        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        paragraph.Append(run);

        var cell = new TableCell(paragraph);
        cell.Append(new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));

        if (widthPct > 0)
        {
            cell.TableCellProperties!.Append(new TableCellWidth
            {
                Width = (widthPct * 50).ToString(),
                Type = TableWidthUnitValues.Pct
            });
        }

        return cell;
    }

    private static void AddParagraph(
        Body body,
        string text,
        bool bold = false,
        JustificationValues? alignment = null)
    {
        var paragraph = new Paragraph();
        if (alignment.HasValue)
        {
            paragraph.Append(new ParagraphProperties(new Justification { Val = alignment.Value }));
        }

        var run = new Run();
        if (bold)
        {
            run.Append(new RunProperties(new Bold()));
        }

        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        paragraph.Append(run);
        body.Append(paragraph);
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("N0", ViCulture);

    private static string FormatRate(decimal rate) =>
        rate % 1 == 0 ? rate.ToString("0", ViCulture) : rate.ToString("0.##", ViCulture);
}
