using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TaxMate.Model.Documents.Tax;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Infrastructure.Documents.Tax;

public sealed class OpenXmlS2bDocumentGenerator : IS2bDocumentGenerator
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private readonly string _templatePath = Path.Combine(
        AppContext.BaseDirectory,
        "Templates",
        "Tax",
        "2026",
        "mau-s2b-hkd.docx");

    public async Task<TaxDeclarationGeneratedFile> GenerateAsync(
        S2bDocumentModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!File.Exists(_templatePath))
            throw new FileNotFoundException("Template S2b-HKD not found.", _templatePath);

        await using var output = new MemoryStream();
        await using (var template = File.OpenRead(_templatePath))
            await template.CopyToAsync(output, cancellationToken);
        output.Position = 0;

        using (var document = WordprocessingDocument.Open(output, true))
        {
            var body = document.MainDocumentPart?.Document.Body
                ?? throw new InvalidOperationException("S2b template has no body.");
            var elements = body.ChildElements
                .Where(x => x is not SectionProperties)
                .ToList();
            if (elements.Count != 7)
                throw new InvalidOperationException("S2b template structure is invalid.");

            FillHeader((Table)elements[0], model);
            SetParagraphLines(
                (Paragraph)elements[1],
                "SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ",
                $"Địa điểm kinh doanh: {model.BusinessLocation}",
                $"Kỳ kê khai: Quý {model.Quarter}/{model.Year}");
            SetParagraphLines((Paragraph)elements[2], "Đơn vị tính: VNĐ");
            FillLedger((Table)elements[3], model.Groups);
            SetParagraphLines(
                (Paragraph)elements[4],
                $"Ngày {model.ExportDate.Day} tháng {model.ExportDate.Month} năm {model.ExportDate.Year}");
            SetParagraphLines(
                (Paragraph)elements[5],
                "NGƯỜI ĐẠI DIỆN HỘ KINH DOANH/",
                "CÁ NHÂN KINH DOANH");
            SetParagraphLines(
                (Paragraph)elements[6],
                "(Ký, ghi rõ họ tên và đóng dấu (nếu có))",
                model.RepresentativeName);
            document.MainDocumentPart!.Document.Save();
        }

        return new TaxDeclarationGeneratedFile
        {
            Content = output.ToArray(),
            FileName = $"S2b-HKD_{model.TaxCode}_Q{model.Quarter}_{model.Year}.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }

    private static void FillHeader(Table header, S2bDocumentModel model)
    {
        SetCellLines(
            header.Descendants<TableCell>().First(),
            $"HỘ, CÁ NHÂN KINH DOANH: {model.BusinessName}",
            $"Địa chỉ: {model.Address}",
            $"Mã số thuế: {model.TaxCode}");
    }

    private static void FillLedger(
        Table table,
        IReadOnlyList<S2bDocumentGroupModel> groups)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count != 24)
            throw new InvalidOperationException("S2b ledger template is invalid.");

        var headingTemplate = Clone(rows[3]);
        var detailTemplate = Clone(rows[4]);
        var totalTemplate = Clone(rows[5]);
        var vatTemplate = Clone(rows[6]);
        var totalVatTemplate = Clone(rows[23]);

        foreach (var row in rows.Skip(3).ToList())
            row.Remove();

        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            var heading = Clone(headingTemplate);
            FillRow(heading, "", "", $"{index + 1}. Ngành nghề {group.BusinessCategoryName}", "");
            table.Append(heading);

            foreach (var line in group.Lines)
            {
                var detail = Clone(detailTemplate);
                FillRow(
                    detail,
                    line.DocumentNumber,
                    line.DocumentDate.ToString("dd/MM/yyyy"),
                    line.Description,
                    Money(line.Amount));
                table.Append(detail);
            }

            var total = Clone(totalTemplate);
            FillRow(total, "", "", $"Tổng cộng ({index + 1})", Money(group.TotalRevenue));
            table.Append(total);

            var vat = Clone(vatTemplate);
            FillRow(vat, "", "", $"Thuế GTGT ({Rate(group.VatRate)}%)", Money(group.VatAmount));
            table.Append(vat);
        }

        var totalVat = Clone(totalVatTemplate);
        FillRow(
            totalVat,
            "",
            "",
            "Tổng số thuế GTGT phải nộp",
            Money(groups.Sum(x => x.VatAmount)));
        table.Append(totalVat);
    }

    private static TableRow Clone(TableRow row) =>
        (TableRow)row.CloneNode(true);

    private static void FillRow(TableRow row, params string[] values)
    {
        var cells = row.Elements<TableCell>().ToList();
        if (cells.Count != values.Length)
            throw new InvalidOperationException("S2b data row does not have 4 cells.");
        for (var index = 0; index < cells.Count; index++)
            SetCellLines(cells[index], values[index]);
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
