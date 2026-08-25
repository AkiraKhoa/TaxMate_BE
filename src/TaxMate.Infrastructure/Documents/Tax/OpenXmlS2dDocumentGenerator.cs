using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Infrastructure.Documents.Tax;

public sealed class OpenXmlS2dDocumentGenerator : IS2dDocumentGenerator
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private readonly string _templatePath = Path.Combine(
        AppContext.BaseDirectory,
        "Templates",
        "Tax",
        "2026",
        "mau-s2d-hkd.docx");

    public async Task<TaxDeclarationGeneratedFile> GenerateAsync(
        S2dDocumentModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(model.Book);
        if (!File.Exists(_templatePath))
            throw new FileNotFoundException("Template S2d-HKD not found.", _templatePath);

        await using var output = new MemoryStream();
        await using (var template = File.OpenRead(_templatePath))
            await template.CopyToAsync(output, cancellationToken);
        output.Position = 0;

        using (var document = WordprocessingDocument.Open(output, true))
        {
            var mainPart = document.MainDocumentPart
                ?? throw new InvalidOperationException("S2d template has no main document part.");
            var body = mainPart.Document.Body
                ?? throw new InvalidOperationException("S2d template has no body.");
            var section = body.GetFirstChild<SectionProperties>()?.CloneNode(true);
            var templateElements = body.ChildElements
                .Where(x => x is not SectionProperties)
                .Select(x => x.CloneNode(true))
                .ToList();
            if (templateElements.Count != 4)
                throw new InvalidOperationException("S2d template structure is invalid.");

            body.RemoveAllChildren();
            IReadOnlyList<S2dItemBook> items = model.Book.Items.Count == 0
                ? new[] { new S2dItemBook { ItemName = "Không có phát sinh" } }
                : model.Book.Items;
            for (var index = 0; index < items.Count; index++)
            {
                if (index > 0)
                    body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));

                var elements = templateElements.Select(x => x.CloneNode(true)).ToList();
                FillItem(elements, model, items[index]);
                foreach (var element in elements)
                    body.Append(element);
            }

            if (section is not null)
                body.Append(section);
            mainPart.Document.Save();
        }

        return new TaxDeclarationGeneratedFile
        {
            Content = output.ToArray(),
            FileName = $"S2d-HKD_{model.TaxCode}_Q{model.Quarter}_{model.Year}.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }

    private static void FillItem(
        IReadOnlyList<OpenXmlElement> elements,
        S2dDocumentModel model,
        S2dItemBook item)
    {
        var header = (Table)elements[0];
        SetCellLines(header.Descendants<TableCell>().First(),
            $"HỘ, CÁ NHÂN KINH DOANH: {model.BusinessName}",
            $"Địa chỉ: {model.Address}",
            $"Mã số thuế: {model.TaxCode}");

        SetParagraphLines((Paragraph)elements[1],
            "SỔ CHI TIẾT VẬT LIỆU, DỤNG CỤ, SẢN PHẨM, HÀNG HÓA",
            $"Tên vật liệu, dụng cụ, sản phẩm, hàng hóa: {item.ItemName}",
            $"Kỳ kê khai: Quý {model.Quarter}/{model.Year}");

        FillLedger((Table)elements[2], item);
        SetCellLines(((Table)elements[3]).Descendants<TableCell>().First(),
            $"Ngày {model.ExportDate.Day} tháng {model.ExportDate.Month} năm {model.ExportDate.Year}",
            "NGƯỜI ĐẠI DIỆN HỘ KINH DOANH/",
            "CÁ NHÂN KINH DOANH",
            "(Ký, ghi rõ họ tên và đóng dấu (nếu có))",
            model.RepresentativeName);
    }

    private static void FillLedger(Table table, S2dItemBook item)
    {
        var rows = table.Elements<TableRow>().ToList();
        var openingTemplate = rows[3].CloneNode(true) as TableRow
            ?? throw new InvalidOperationException("S2d opening row is missing.");
        var detailTemplate = rows[4].CloneNode(true) as TableRow
            ?? throw new InvalidOperationException("S2d detail row is missing.");
        var totalTemplate = rows[10].CloneNode(true) as TableRow
            ?? throw new InvalidOperationException("S2d total row is missing.");
        var endingTemplate = rows[11].CloneNode(true) as TableRow
            ?? throw new InvalidOperationException("S2d ending row is missing.");
        foreach (var row in rows.Skip(3).ToList())
            row.Remove();

        FillRow(openingTemplate,
            "", "", "Số dư đầu kỳ", item.Unit ?? "",
            Unit(item.OpeningValue, item.OpeningQuantity), "", "", "", "",
            Number(item.OpeningQuantity), Money(item.OpeningValue), "");
        table.Append(openingTemplate);

        foreach (var line in item.Lines)
        {
            var row = (TableRow)detailTemplate.CloneNode(true);
            FillRow(row,
                line.DocumentNumber,
                line.DocumentDate.ToString("dd/MM/yyyy"),
                line.Description,
                item.Unit ?? "",
                Money(line.InboundUnitValue ?? line.OutboundUnitValue),
                Number(line.InboundQuantity),
                Money(line.InboundValue),
                Number(line.OutboundQuantity),
                Money(line.OutboundValue),
                Number(line.RunningQuantity),
                Money(line.RunningValue),
                line.IsProvisionalValue ? "Tạm tính" : "");
            table.Append(row);
        }

        FillRow(totalTemplate,
            "", "", "Cộng phát sinh trong kỳ", "X", "X",
            Number(item.TotalInboundQuantity), Money(item.TotalInboundValue),
            Number(item.TotalOutboundQuantity), Money(item.TotalOutboundValue), "", "", "");
        table.Append(totalTemplate);
        FillRow(endingTemplate,
            "", "", "Số dư cuối kỳ", item.Unit ?? "",
            Unit(item.EndingValue, item.EndingQuantity), "X", "X", "X", "X",
            Number(item.EndingQuantity), Money(item.EndingValue), "");
        table.Append(endingTemplate);
    }

    private static void FillRow(TableRow row, params string[] values)
    {
        var cells = row.Elements<TableCell>().ToList();
        if (cells.Count != values.Length)
            throw new InvalidOperationException("S2d data row does not have 12 cells.");
        for (var i = 0; i < cells.Count; i++)
            SetCellLines(cells[i], values[i]);
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
        foreach (var existingRun in paragraph.Elements<Run>().ToList())
            existingRun.Remove();
        var run = new Run();
        if (runProperties is not null)
            run.Append(runProperties);
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                run.Append(new Break());
            run.Append(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        paragraph.Append(run);
    }

    private static string Number(decimal? value) =>
        !value.HasValue ? "" : value.Value.ToString("#,##0.###", Vietnamese);

    private static string Money(decimal? value) =>
        !value.HasValue ? "" : value.Value.ToString("#,##0.##", Vietnamese);

    private static string Unit(decimal value, decimal quantity) =>
        quantity == 0m ? "" : Money(value / quantity);
}
