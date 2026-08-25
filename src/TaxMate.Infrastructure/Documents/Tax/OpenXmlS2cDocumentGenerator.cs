using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TaxMate.Model.Documents.Tax;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Infrastructure.Documents.Tax;

public sealed class OpenXmlS2cDocumentGenerator : IS2cDocumentGenerator
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private readonly string _templatePath = Path.Combine(
        AppContext.BaseDirectory,
        "Templates",
        "Tax",
        "2026",
        "mau-s2c-hkd.docx");

    public async Task<TaxDeclarationGeneratedFile> GenerateAsync(
        S2cDocumentModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!File.Exists(_templatePath))
            throw new FileNotFoundException("Template S2c-HKD not found.", _templatePath);

        await using var output = new MemoryStream();
        await using (var template = File.OpenRead(_templatePath))
            await template.CopyToAsync(output, cancellationToken);
        output.Position = 0;

        using (var document = WordprocessingDocument.Open(output, true))
        {
            var body = document.MainDocumentPart?.Document.Body
                ?? throw new InvalidOperationException("S2c template has no body.");
            var elements = body.ChildElements
                .Where(x => x is not SectionProperties)
                .ToList();
            if (elements.Count != 10)
                throw new InvalidOperationException("S2c template structure is invalid.");

            FillHeader((Table)elements[0], model);
            SetParagraphLines((Paragraph)elements[1], "SỔ CHI TIẾT DOANH THU, CHI PHÍ");
            SetParagraphLines((Paragraph)elements[2], $"Tên địa điểm kinh doanh: {model.BusinessLocation}");
            SetParagraphLines((Paragraph)elements[3], $"Kỳ kê khai: Quý {model.Quarter}/{model.Year}");
            SetParagraphLines((Paragraph)elements[4], "Đơn vị tính: VNĐ");
            FillLedger((Table)elements[5], model);
            SetParagraphLines(
                (Paragraph)elements[6],
                $"Ngày {model.ExportDate.Day} tháng {model.ExportDate.Month} năm {model.ExportDate.Year}");
            SetParagraphLines((Paragraph)elements[7], "NGƯỜI ĐẠI DIỆN HỘ KINH DOANH/");
            SetParagraphLines((Paragraph)elements[8], "CÁ NHÂN KINH DOANH");
            SetParagraphLines(
                (Paragraph)elements[9],
                "(Ký, ghi rõ họ tên và đóng dấu (nếu có))",
                model.RepresentativeName);
            document.MainDocumentPart!.Document.Save();
        }

        return new TaxDeclarationGeneratedFile
        {
            Content = output.ToArray(),
            FileName = $"S2c-HKD_{model.TaxCode}_Q{model.Quarter}_{model.Year}.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }

    private static void FillHeader(Table header, S2cDocumentModel model)
    {
        SetCellLines(
            header.Descendants<TableCell>().First(),
            $"HỘ, CÁ NHÂN KINH DOANH: {model.BusinessName}",
            $"Địa chỉ: {model.Address}",
            $"Mã số thuế: {model.TaxCode}");
    }

    private static void FillLedger(Table table, S2cDocumentModel model)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count != 13)
            throw new InvalidOperationException("S2c ledger template is invalid.");

        SetAmount(rows[3], model.Revenue);
        SetAmount(rows[4], model.TotalExpense);
        SetAmount(rows[5], model.MaterialCost);
        SetAmount(rows[6], model.LaborCost);
        SetAmount(rows[7], model.DepreciationCost);
        SetAmount(rows[8], model.PurchasedServicesCost);
        SetAmount(rows[9], model.LoanInterestCost);
        SetAmount(rows[10], model.OtherDirectCost);
        SetAmount(rows[11], model.NetIncome);
        SetAmount(rows[12], model.PitAmount);

        if (model.PitRate.HasValue)
        {
            var cells = rows[12].Elements<TableCell>().ToList();
            SetCellLines(
                cells[2],
                $"4. Tổng số thuế TNCN phải nộp {{(4) = (3) x {Rate(model.PitRate.Value)}%}}");
        }
    }

    private static void SetAmount(TableRow row, decimal? value)
    {
        var cells = row.Elements<TableCell>().ToList();
        if (cells.Count != 4)
            throw new InvalidOperationException("S2c data row does not have 4 cells.");
        SetCellLines(cells[3], value.HasValue ? Money(value.Value) : string.Empty);
    }

    private static void SetCellLines(TableCell cell, params string[] lines)
    {
        var paragraphs = cell.Elements<Paragraph>().ToList();
        var prototype = paragraphs.FirstOrDefault()?.CloneNode(true) as Paragraph
            ?? new Paragraph(new Run(new Text()));
        foreach (var paragraph in paragraphs)
            paragraph.Remove();
        foreach (var line in lines)
        {
            var paragraph = (Paragraph)prototype.CloneNode(true);
            SetParagraphLines(paragraph, line);
            cell.Append(paragraph);
        }
    }

    private static void SetParagraphLines(Paragraph paragraph, params string[] lines)
    {
        var runProperties = paragraph.Descendants<RunProperties>().FirstOrDefault()?.CloneNode(true);
        foreach (var run in paragraph.Elements<Run>().ToList())
            run.Remove();
        var replacement = new Run();
        if (runProperties is not null)
            replacement.Append(runProperties);
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
                replacement.Append(new Break());
            replacement.Append(new Text(lines[index]) { Space = SpaceProcessingModeValues.Preserve });
        }
        paragraph.Append(replacement);
    }

    private static string Money(decimal value) =>
        value.ToString("#,##0.##", Vietnamese);

    private static string Rate(decimal value) =>
        value.ToString("0.##", Vietnamese);
}
