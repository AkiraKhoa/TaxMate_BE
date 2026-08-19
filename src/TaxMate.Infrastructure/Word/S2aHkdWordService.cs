using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure.Word;

/// <summary>
/// Fills the official Mẫu số S2a-HKD Word template (mau-s2a-HKD.docx).
/// </summary>
public class S2aHkdWordService : IS2aHkdWordService
{
    private const string TemplateFileName = "mau-s2a-HKD.docx";
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public Task<byte[]> GenerateDocxAsync(IReadOnlyList<S2aHkdDocumentModel> models)
    {
        if (models is null || models.Count == 0)
            throw new ArgumentException("At least one S2a-HKD document is required.", nameof(models));

        var templateBytes = LoadTemplateBytes();
        using var stream = new MemoryStream();
        stream.Write(templateBytes, 0, templateBytes.Length);
        stream.Position = 0;

        using (var document = WordprocessingDocument.Open(stream, true))
        {
            var body = document.MainDocumentPart?.Document.Body
                ?? throw new InvalidOperationException("S2a-HKD template has no document body.");

            var sectPr = body.Elements<SectionProperties>().LastOrDefault();
            var templateNodes = body.ChildElements
                .Where(e => e is not SectionProperties)
                .Select(e => e.CloneNode(true))
                .ToList();

            body.RemoveAllChildren();

            for (var i = 0; i < models.Count; i++)
            {
                if (i > 0)
                {
                    body.AppendChild(new Paragraph(
                        new Run(new Break { Type = BreakValues.Page })));
                }

                var added = new List<OpenXmlElement>();
                foreach (var node in templateNodes)
                {
                    var clone = node.CloneNode(true);
                    body.AppendChild(clone);
                    added.Add(clone);
                }

                FillBusinessDocument(added, models[i]);
            }

            if (sectPr is not null)
                body.AppendChild((OpenXmlElement)sectPr.CloneNode(true));

            document.MainDocumentPart!.Document.Save();
        }

        return Task.FromResult(stream.ToArray());
    }

    private static void FillBusinessDocument(
        List<OpenXmlElement> elements,
        S2aHkdDocumentModel model)
    {
        var tables = elements.OfType<Table>().ToList();
        if (tables.Count < 2)
            throw new InvalidOperationException("S2a-HKD template is missing expected tables.");

        var paragraphs = elements.OfType<Paragraph>().ToList();
        FillHeaderTable(tables[0], model.Header);
        FillHeaderParagraphs(paragraphs, model.Header);
        FillMainTable(tables[1], model);
        FillExportDate(paragraphs, model.Footer.ExportDate);
    }

    private static byte[] LoadTemplateBytes()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Word", TemplateFileName),
            Path.Combine(AppContext.BaseDirectory, TemplateFileName),
            Path.Combine(
                Path.GetDirectoryName(typeof(S2aHkdWordService).Assembly.Location)!,
                "Word",
                TemplateFileName)
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return File.ReadAllBytes(path);
        }

        throw new FileNotFoundException(
            $"Official S2a-HKD template '{TemplateFileName}' was not found. " +
            $"Looked in: {string.Join("; ", candidates)}");
    }

    private static void FillHeaderTable(Table headerTable, S2aHkdHeaderModel header)
    {
        var firstCell = headerTable
            .Elements<TableRow>().FirstOrDefault()
            ?.Elements<TableCell>().FirstOrDefault();

        if (firstCell is null)
            return;

        var paragraphs = firstCell.Elements<Paragraph>().ToList();
        if (paragraphs.Count >= 1)
            SetParagraphText(paragraphs[0], $"HỘ, CÁ NHÂN KINH DOANH: {header.BusinessName}");
        if (paragraphs.Count >= 2)
            SetParagraphText(paragraphs[1], $"Địa chỉ: {header.Address}");
        if (paragraphs.Count >= 3)
            SetParagraphText(paragraphs[2], $"Mã số thuế: {header.TaxCode}");
    }

    private static void FillHeaderParagraphs(IEnumerable<Paragraph> paragraphs, S2aHkdHeaderModel header)
    {
        foreach (var paragraph in paragraphs)
        {
            var text = GetParagraphText(paragraph);
            if (text.StartsWith("Địa điểm kinh doanh:", StringComparison.Ordinal))
            {
                SetParagraphText(paragraph, $"Địa điểm kinh doanh: {header.Address}");
            }
            else if (text.StartsWith("Kỳ kê khai:", StringComparison.Ordinal))
            {
                SetParagraphText(paragraph, $"Kỳ kê khai: {header.DeclarationPeriod}");
            }
            else if (text.StartsWith("Đơn vị tính:", StringComparison.Ordinal))
            {
                SetParagraphText(paragraph, $"Đơn vị tính: {header.Unit}");
            }
        }
    }

    private static void FillExportDate(IEnumerable<Paragraph> paragraphs, DateTime exportDate)
    {
        foreach (var paragraph in paragraphs)
        {
            var text = GetParagraphText(paragraph);
            if (text.Contains("tháng", StringComparison.Ordinal) &&
                text.Contains("năm", StringComparison.Ordinal) &&
                (text.StartsWith("Ngày", StringComparison.Ordinal) || text.Contains('…')))
            {
                SetParagraphText(
                    paragraph,
                    $"Ngày {exportDate.Day} tháng {exportDate.Month} năm {exportDate.Year}");
                return;
            }
        }
    }

    private static void FillMainTable(Table mainTable, S2aHkdDocumentModel model)
    {
        var rows = mainTable.Elements<TableRow>().ToList();
        if (rows.Count < 8)
            throw new InvalidOperationException("S2a-HKD main table is incomplete.");

        // Prototype rows from the first ngành nghề block + footer rows.
        var sectionProto = (TableRow)rows[3].CloneNode(true);
        var detailProto = (TableRow)rows[4].CloneNode(true);
        var subtotalProto = (TableRow)rows[5].CloneNode(true);
        var vatProto = (TableRow)rows[6].CloneNode(true);
        var pitProto = (TableRow)rows[7].CloneNode(true);
        var totalVatProto = (TableRow)rows[^2].CloneNode(true);
        var totalPitProto = (TableRow)rows[^1].CloneNode(true);

        // Keep header rows 0–2; remove data/footer rows.
        foreach (var row in rows.Skip(3).ToList())
            row.Remove();

        foreach (var group in model.Groups)
        {
            var sectionRow = (TableRow)sectionProto.CloneNode(true);
            SetCellText(GetCell(sectionRow, 2), $"{group.GroupNumber}. Ngành nghề: {group.CategoryName}");
            mainTable.AppendChild(sectionRow);

            foreach (var line in group.Lines)
            {
                var detailRow = (TableRow)detailProto.CloneNode(true);
                SetCellText(GetCell(detailRow, 0), line.DocumentNumber);
                SetCellText(GetCell(detailRow, 1), line.TransactionDate.ToString("dd/MM/yyyy", ViCulture));
                SetCellText(GetCell(detailRow, 2), line.Description);
                SetCellText(GetCell(detailRow, 3), FormatAmount(line.Amount), JustificationValues.Right);
                mainTable.AppendChild(detailRow);
            }

            var subtotalRow = (TableRow)subtotalProto.CloneNode(true);
            SetCellText(GetCell(subtotalRow, 2), $"Tổng cộng ({group.GroupNumber})");
            SetCellText(GetCell(subtotalRow, 3), FormatAmount(group.Subtotal), JustificationValues.Right);
            mainTable.AppendChild(subtotalRow);

            var vatRow = (TableRow)vatProto.CloneNode(true);
            SetCellText(GetCell(vatRow, 2), $"Thuế GTGT ({FormatRate(group.VatRate)}%)");
            SetCellText(GetCell(vatRow, 3), FormatAmount(group.VatTax), JustificationValues.Right);
            mainTable.AppendChild(vatRow);

            var pitRow = (TableRow)pitProto.CloneNode(true);
            SetCellText(GetCell(pitRow, 2), $"Thuế TNCN ({FormatRate(group.PitRate)}%)");
            SetCellText(GetCell(pitRow, 3), FormatAmount(group.PitTax), JustificationValues.Right);
            mainTable.AppendChild(pitRow);
        }

        var totalVatRow = (TableRow)totalVatProto.CloneNode(true);
        SetCellText(GetCell(totalVatRow, 2), "Tổng số thuế GTGT phải trả");
        SetCellText(GetCell(totalVatRow, 3), FormatAmount(model.Footer.TotalVatTax), JustificationValues.Right);
        mainTable.AppendChild(totalVatRow);

        var totalPitRow = (TableRow)totalPitProto.CloneNode(true);
        SetCellText(GetCell(totalPitRow, 2), "Tổng số thuế TNCN phải trả");
        SetCellText(GetCell(totalPitRow, 3), FormatAmount(model.Footer.TotalPitTax), JustificationValues.Right);
        mainTable.AppendChild(totalPitRow);
    }

    private static TableCell GetCell(TableRow row, int index)
    {
        var cells = row.Elements<TableCell>().ToList();
        if (index < 0 || index >= cells.Count)
            throw new InvalidOperationException($"Table row does not have cell index {index}.");
        return cells[index];
    }

    private static void SetCellText(
        TableCell cell,
        string text,
        JustificationValues? alignment = null)
    {
        var paragraph = cell.Elements<Paragraph>().FirstOrDefault();
        if (paragraph is null)
        {
            paragraph = new Paragraph();
            cell.AppendChild(paragraph);
        }

        if (alignment.HasValue)
        {
            var pPr = paragraph.GetFirstChild<ParagraphProperties>() ?? paragraph.PrependChild(new ParagraphProperties());
            var jc = pPr.GetFirstChild<Justification>();
            if (jc is null)
                pPr.AppendChild(new Justification { Val = alignment.Value });
            else
                jc.Val = alignment.Value;
        }

        SetParagraphText(paragraph, text);
    }

    private static void SetParagraphText(Paragraph paragraph, string text)
    {
        var existingRunProps = paragraph
            .Elements<Run>()
            .Select(r => r.RunProperties?.CloneNode(true) as RunProperties)
            .FirstOrDefault(r => r is not null)
            ?? paragraph.ParagraphProperties?.GetFirstChild<RunProperties>()?.CloneNode(true) as RunProperties;

        paragraph.RemoveAllChildren<Run>();

        var run = new Run();
        if (existingRunProps is not null)
            run.AppendChild(existingRunProps);

        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        paragraph.AppendChild(run);
    }

    private static string GetParagraphText(Paragraph paragraph)
    {
        var sb = new StringBuilder();
        foreach (var text in paragraph.Descendants<Text>())
            sb.Append(text.Text);
        return sb.ToString();
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("N0", ViCulture);

    private static string FormatRate(decimal rate) =>
        rate % 1 == 0 ? rate.ToString("0", ViCulture) : rate.ToString("0.##", ViCulture);
}
