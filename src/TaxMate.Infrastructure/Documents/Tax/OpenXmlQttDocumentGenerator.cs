using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TaxMate.Model.Documents.Tax;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Infrastructure.Documents.Tax;

public sealed class OpenXmlQttDocumentGenerator : IQttDocumentGenerator
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private readonly string _templatePath = Path.Combine(
        AppContext.BaseDirectory,
        "Templates",
        "Tax",
        "2026",
        "mau-02-cnkd-tncn-qtt.docx");

    public async Task<TaxDeclarationGeneratedFile> GenerateAsync(
        QttDocumentModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!File.Exists(_templatePath))
            throw new FileNotFoundException("Template 02/CNKD-TNCN-QTT not found.", _templatePath);

        await using var output = new MemoryStream();
        await using (var template = File.OpenRead(_templatePath))
            await template.CopyToAsync(output, cancellationToken);
        output.Position = 0;

        using (var document = WordprocessingDocument.Open(output, true))
        {
            var body = document.MainDocumentPart?.Document.Body
                ?? throw new InvalidOperationException("QTT template has no body.");
            ReplaceTokens(body, model);

            var tables = body.Elements<Table>().ToList();
            if (tables.Count != 5)
                throw new InvalidOperationException("QTT template structure is invalid.");

            var i = model.Snapshot.Indicators;
            FillValueRows(tables[0],
                Money(i.Indicator09), Money(i.Indicator09a), Money(i.Indicator09b),
                Money(i.Indicator09c), Money(i.Indicator10), Money(i.Indicator10a),
                Money(i.Indicator10b), Money(i.Indicator10c), Money(i.Indicator10d),
                Money(i.Indicator10LoanInterest), Money(i.Indicator10e), Money(i.Indicator11),
                Rate(i.Indicator12Rate), Money(i.Indicator13), Money(i.Indicator14),
                Money(i.Indicator15), Money(i.Indicator16), Money(i.Indicator17),
                Money(i.Indicator18), Money(i.Indicator19),
                Money(i.Indicator20), Money(i.Indicator21), Money(i.Indicator22),
                Money(i.Indicator23), Money(i.Indicator24));
            FillInventory(tables[1], model);
            FillPaymentSupport(tables[2], model);
            FillOffsets(tables[3], model);

            document.MainDocumentPart!.Document.Save();
        }

        return new TaxDeclarationGeneratedFile
        {
            Content = output.ToArray(),
            FileName = $"02-CNKD-TNCN-QTT_{model.Snapshot.TaxCode}_{model.Snapshot.TaxYear}.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }

    private static void ReplaceTokens(Body body, QttDocumentModel model)
    {
        var snapshot = model.Snapshot;
        var account = snapshot.RefundAccount;
        var replacements = new Dictionary<string, string>
        {
            ["{{YEAR}}"] = snapshot.TaxYear.ToString(CultureInfo.InvariantCulture),
            ["{{TAXPAYER_NAME}}"] = snapshot.TaxpayerName,
            ["{{TAX_CODE}}"] = snapshot.TaxCode,
            ["{{ADDRESS}}"] = snapshot.TaxpayerAddress ?? string.Empty,
            ["{{REFUND_ACCOUNT_NAME}}"] = account?.AccountName ?? string.Empty,
            ["{{REFUND_ACCOUNT_NUMBER}}"] = account?.AccountNumber ?? string.Empty,
            ["{{REFUND_BANK_NAME}}"] = account?.BankName ?? string.Empty,
            ["{{EXPORT_DATE}}"] = $"Ngày {model.ExportDate.Day} tháng {model.ExportDate.Month} năm {model.ExportDate.Year}"
        };

        foreach (var text in body.Descendants<Text>())
            if (replacements.TryGetValue(text.Text, out var value))
                text.Text = value;
    }

    private static void FillValueRows(Table table, params string[] values)
    {
        var rows = table.Elements<TableRow>().Skip(1).ToList();
        if (rows.Count != values.Length)
            throw new InvalidOperationException("QTT indicator table structure is invalid.");
        for (var index = 0; index < rows.Count; index++)
            SetCellLines(rows[index].Elements<TableCell>().Last(), values[index], JustificationValues.Right, "20");
    }

    private static void FillInventory(Table table, QttDocumentModel model)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count != 4)
            throw new InvalidOperationException("QTT inventory table structure is invalid.");
        var template = Clone(rows[2]);
        var total = rows[3];
        rows[2].Remove();

        var inventoryRows = model.Snapshot.InventoryRows;
        if (inventoryRows.Count == 0)
            inventoryRows = [new(Guid.Empty, null, null, string.Empty, "Hàng tồn kho", 0m, 0m, 0m, 0m)];
        for (var index = 0; index < inventoryRows.Count; index++)
        {
            var item = inventoryRows[index];
            var row = Clone(template);
            FillRow(row,
                [JustificationValues.Center, JustificationValues.Left, JustificationValues.Right, JustificationValues.Right, JustificationValues.Right, JustificationValues.Right],
                (index + 1).ToString(CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(item.ItemCode)
                    ? item.ItemName
                    : $"{item.ItemCode} - {item.ItemName}",
                Money(item.OpeningValue), Money(item.InboundValue),
                Money(item.OutboundValue), Money(item.EndingValue));
            table.InsertBefore(row, total);
        }

        var totals = model.Snapshot.InventoryTotals;
        var totalCells = total.Elements<TableCell>().ToList();
        var amounts = new[] { totals.Indicator31, totals.Indicator32, totals.Indicator33, totals.Indicator34 };
        for (var index = 0; index < amounts.Length; index++)
            SetCellLines(totalCells[index + 2], $"[{31 + index}] {Money(amounts[index])}", JustificationValues.Right, "20");
    }

    private static void FillPaymentSupport(Table table, QttDocumentModel model)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count != 5)
            throw new InvalidOperationException("QTT payment-support table structure is invalid.");
        var template = Clone(rows[2]);
        var total = rows[4];
        rows[2].Remove();
        rows[3].Remove();

        for (var index = 0; index < model.PaymentSupportRows.Count; index++)
        {
            var item = model.PaymentSupportRows[index];
            var row = Clone(template);
            FillRow(row,
                [JustificationValues.Center, JustificationValues.Left, JustificationValues.Right,
                 JustificationValues.Center, JustificationValues.Center, JustificationValues.Center,
                 JustificationValues.Left, JustificationValues.Left, JustificationValues.Center],
                (index + 1).ToString(CultureInfo.InvariantCulture),
                item.BudgetContent,
                Money(item.Amount),
                item.ChapterCode ?? string.Empty,
                item.SubsectionCode ?? string.Empty,
                item.AdministrativeAreaCode ?? string.Empty,
                item.CollectingAuthority ?? string.Empty,
                item.TaxAuthority ?? string.Empty,
                Date(item.DueDate));
            table.InsertBefore(row, total);
        }

        // The official total merges the first two grid columns. Its second
        // physical cell is the amount column, carrying printed indicator [44].
        var totalCells = total.Elements<TableCell>().ToList();
        if (totalCells.Count != 8 || totalCells[0].TableCellProperties?.GridSpan?.Val?.Value != 2)
            throw new InvalidOperationException("QTT payment total structure is invalid.");
        SetCellLines(totalCells[1], $"[44] {Money(model.PaymentSupportRows.Sum(x => x.Amount))}", JustificationValues.Right);
    }

    private static void FillOffsets(Table table, QttDocumentModel model)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count != 5)
            throw new InvalidOperationException("QTT offset table structure is invalid.");
        var template = Clone(rows[3]);
        rows[3].Remove();
        rows[4].Remove();

        for (var index = 0; index < model.Snapshot.OffsetItems.Count; index++)
        {
            var item = model.Snapshot.OffsetItems[index];
            var row = Clone(template);
            FillRow(row,
                [JustificationValues.Center, JustificationValues.Center, JustificationValues.Left,
                 JustificationValues.Center, JustificationValues.Left, JustificationValues.Center,
                 JustificationValues.Center, JustificationValues.Left, JustificationValues.Center,
                 JustificationValues.Center, JustificationValues.Right, JustificationValues.Right, JustificationValues.Right],
                (index + 1).ToString(CultureInfo.InvariantCulture),
                item.TaxCode,
                item.TaxpayerName,
                item.ObligationIdentifier,
                item.BudgetContent,
                item.ChapterCode ?? string.Empty,
                item.SubsectionCode ?? string.Empty,
                item.CollectingAuthority ?? string.Empty,
                item.AdministrativeAreaCode ?? string.Empty,
                Date(item.DueDate),
                Money(item.OutstandingAmount),
                Money(item.OffsetAmount),
                Money(item.RemainingAmount));
            table.Append(row);
        }
    }

    private static TableRow Clone(TableRow row) => (TableRow)row.CloneNode(true);

    private static void FillRow(TableRow row, JustificationValues[] alignments, params string[] values)
    {
        var cells = row.Elements<TableCell>().ToList();
        if (cells.Count != values.Length || alignments.Length != values.Length)
            throw new InvalidOperationException("QTT data row has an unexpected number of cells.");
        for (var index = 0; index < values.Length; index++)
            SetCellLines(cells[index], values[index], alignments[index]);
    }

    private static void SetCellLines(TableCell cell, JustificationValues? alignment, string fontSize, params string[] lines)
    {
        var paragraphs = cell.Elements<Paragraph>().ToList();
        var prototype = paragraphs.FirstOrDefault()?.CloneNode(true) as Paragraph
            ?? new Paragraph(new Run(new Text()));
        foreach (var paragraph in paragraphs)
            paragraph.Remove();
        foreach (var line in lines)
        {
            var paragraph = (Paragraph)prototype.CloneNode(true);
            SetParagraphLines(paragraph, alignment, fontSize, line);
            cell.Append(paragraph);
        }
    }

    private static void SetCellLines(TableCell cell, string line, JustificationValues? alignment = null, string fontSize = "18")
    {
        SetCellLines(cell, alignment, fontSize, new[] { line });
    }

    private static void SetParagraphLines(Paragraph paragraph, params string[] lines)
    {
        SetParagraphLines(paragraph, null, "18", lines);
    }

    private static void SetParagraphLines(Paragraph paragraph, JustificationValues? alignment, string fontSize, params string[] lines)
    {
        if (alignment.HasValue)
        {
            var pPr = paragraph.GetFirstChild<ParagraphProperties>() ?? paragraph.PrependChild(new ParagraphProperties());
            var jc = pPr.GetFirstChild<Justification>();
            if (jc is null)
                pPr.AppendChild(new Justification { Val = alignment.Value });
            else
                jc.Val = alignment.Value;
        }

        var runProperties = paragraph.Descendants<RunProperties>().FirstOrDefault()?.CloneNode(true) ?? new RunProperties();
        var sz = runProperties.GetFirstChild<FontSize>();
        if (sz is null)
            runProperties.AppendChild(new FontSize { Val = fontSize });
        else
            sz.Val = fontSize;

        var szCs = runProperties.GetFirstChild<FontSizeComplexScript>();
        if (szCs is null)
            runProperties.AppendChild(new FontSizeComplexScript { Val = fontSize });
        else
            szCs.Val = fontSize;

        foreach (var existingRun in paragraph.Elements<Run>().ToList())
            existingRun.Remove();
        var run = new Run();
        run.Append(runProperties);
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                run.Append(new Break());
            run.Append(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        paragraph.Append(run);
    }

    private static string Money(decimal value) => value.ToString("#,##0.##", Vietnamese);
    private static string Rate(decimal value) => $"{value.ToString("0.##", Vietnamese)}%";
    private static string Date(DateTime? value) => value?.ToString("dd/MM/yyyy") ?? string.Empty;
}
