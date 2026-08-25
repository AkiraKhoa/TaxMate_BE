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

            var tables = body.Descendants<Table>().ToList();
            if (tables.Count != 6)
                throw new InvalidOperationException("QTT template structure is invalid.");

            var i = model.Snapshot.Indicators;
            FillValueRows(tables[0],
                Money(i.Indicator09), Money(i.Indicator09a), Money(i.Indicator09b));
            FillValueRows(tables[1],
                Money(i.Indicator09c), Money(i.Indicator10), Money(i.Indicator10a),
                Money(i.Indicator10b), Money(i.Indicator10c), Money(i.Indicator10d),
                Money(i.Indicator10LoanInterest), Money(i.Indicator10e), Money(i.Indicator11),
                Rate(i.Indicator12Rate), Money(i.Indicator13), Money(i.Indicator14),
                Money(i.Indicator15), Money(i.Indicator16), Money(i.Indicator17),
                Money(i.Indicator18), Money(i.Indicator19));
            FillValueRows(tables[2],
                Money(i.Indicator20), Money(i.Indicator21), Money(i.Indicator22),
                Money(i.Indicator23), Money(i.Indicator24));
            FillInventory(tables[3], model);
            FillPaymentSupport(tables[4], model);
            FillOffsets(tables[5], model);

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
            SetCell(rows[index].Elements<TableCell>().Last(), values[index]);
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
                (index + 1).ToString(CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(item.ItemCode)
                    ? item.ItemName
                    : $"{item.ItemCode} - {item.ItemName}",
                Money(item.OpeningValue), Money(item.InboundValue),
                Money(item.OutboundValue), Money(item.EndingValue));
            table.InsertBefore(row, total);
        }

        var totals = model.Snapshot.InventoryTotals;
        FillRow(total, string.Empty, "Tổng cộng",
            Money(totals.Indicator31), Money(totals.Indicator32),
            Money(totals.Indicator33), Money(totals.Indicator34));
    }

    private static void FillPaymentSupport(Table table, QttDocumentModel model)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count != 4)
            throw new InvalidOperationException("QTT payment-support table structure is invalid.");
        var template = Clone(rows[2]);
        var total = rows[3];
        rows[2].Remove();

        for (var index = 0; index < model.PaymentSupportRows.Count; index++)
        {
            var item = model.PaymentSupportRows[index];
            var row = Clone(template);
            FillRow(row,
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

        FillRow(total, "Tổng cộng", Money(model.PaymentSupportRows.Sum(x => x.Amount)),
            string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty);
    }

    private static void FillOffsets(Table table, QttDocumentModel model)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count != 3)
            throw new InvalidOperationException("QTT offset table structure is invalid.");
        var template = Clone(rows[2]);
        rows[2].Remove();

        for (var index = 0; index < model.Snapshot.OffsetItems.Count; index++)
        {
            var item = model.Snapshot.OffsetItems[index];
            var row = Clone(template);
            FillRow(row,
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

    private static void FillRow(TableRow row, params string[] values)
    {
        var cells = row.Elements<TableCell>().ToList();
        if (cells.Count != values.Length)
            throw new InvalidOperationException("QTT data row has an unexpected number of cells.");
        for (var index = 0; index < values.Length; index++)
            SetCell(cells[index], values[index]);
    }

    private static void SetCell(TableCell cell, string value)
    {
        var paragraph = cell.Elements<Paragraph>().FirstOrDefault()
            ?? cell.AppendChild(new Paragraph());
        var runProperties = paragraph.Descendants<RunProperties>().FirstOrDefault()?.CloneNode(true);
        paragraph.RemoveAllChildren<Run>();
        var run = new Run();
        if (runProperties is not null)
            run.Append(runProperties);
        run.Append(new Text(value) { Space = SpaceProcessingModeValues.Preserve });
        paragraph.Append(run);
    }

    private static string Money(decimal value) => value.ToString("#,##0.##", Vietnamese);
    private static string Rate(decimal value) => $"{value.ToString("0.##", Vietnamese)}%";
    private static string Date(DateTime? value) => value?.ToString("dd/MM/yyyy") ?? string.Empty;
}
