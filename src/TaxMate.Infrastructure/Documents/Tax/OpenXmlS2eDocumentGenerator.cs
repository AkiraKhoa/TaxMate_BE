using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TaxMate.Model.Common;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Infrastructure.Documents.Tax;

public sealed class OpenXmlS2eDocumentGenerator : IS2eDocumentGenerator
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private readonly string _templatePath = Path.Combine(
        AppContext.BaseDirectory,
        "Templates",
        "Tax",
        "2026",
        "mau-s2e-hkd.docx");

    public async Task<TaxDeclarationGeneratedFile> GenerateAsync(
        S2eDocumentModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(model.Book);
        if (!File.Exists(_templatePath))
            throw new FileNotFoundException("Template S2e-HKD not found.", _templatePath);

        await using var output = new MemoryStream();
        await using (var template = File.OpenRead(_templatePath))
            await template.CopyToAsync(output, cancellationToken);
        output.Position = 0;

        using (var document = WordprocessingDocument.Open(output, true))
        {
            var body = document.MainDocumentPart?.Document.Body
                ?? throw new InvalidOperationException("S2e template has no body.");
            var elements = body.ChildElements
                .Where(x => x is not SectionProperties)
                .ToList();
            if (elements.Count != 5)
                throw new InvalidOperationException("S2e template structure is invalid.");

            FillHeader((Table)elements[0], model);
            SetParagraphLines(
                (Paragraph)elements[1],
                "SỔ CHI TIẾT TIỀN",
                $"Kỳ kê khai: Quý {model.Quarter}/{model.Year}");
            SetParagraphLines((Paragraph)elements[2], "Đơn vị tính: VNĐ");
            FillLedger((Table)elements[3], model.Book.Accounts);
            SetCellLines(
                ((Table)elements[4]).Descendants<TableCell>().First(),
                $"Ngày {model.ExportDate.Day} tháng {model.ExportDate.Month} năm {model.ExportDate.Year}",
                "NGƯỜI ĐẠI DIỆN HỘ KINH DOANH/",
                "CÁ NHÂN KINH DOANH",
                "(Ký, ghi rõ họ tên và đóng dấu (nếu có))",
                model.RepresentativeName);
            document.MainDocumentPart!.Document.Save();
        }

        return new TaxDeclarationGeneratedFile
        {
            Content = output.ToArray(),
            FileName = $"S2e-HKD_{model.TaxCode}_Q{model.Quarter}_{model.Year}.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }

    private static void FillHeader(Table header, S2eDocumentModel model)
    {
        SetCellLines(
            header.Descendants<TableCell>().First(),
            $"HỘ, CÁ NHÂN KINH DOANH: {model.BusinessName}",
            $"Địa chỉ: {model.Address}",
            $"Mã số thuế: {model.TaxCode}");
    }

    private static void FillLedger(
        Table table,
        IReadOnlyList<S2eAccountSection> accounts)
    {
        var rows = table.Elements<TableRow>().ToList();
        var cashHeading = Clone(rows[3]);
        var cashOpening = Clone(rows[4]);
        var cashDetail = Clone(rows[5]);
        var cashTotalIn = Clone(rows[7]);
        var cashTotalOut = Clone(rows[8]);
        var cashEnding = Clone(rows[9]);
        var bankGroupHeading = Clone(rows[10]);
        var bankHeading = Clone(rows[11]);
        var bankOpening = Clone(rows[12]);
        var bankDetail = Clone(rows[13]);
        var bankTotalIn = Clone(rows[15]);
        var bankTotalOut = Clone(rows[16]);
        var bankEnding = Clone(rows[17]);

        foreach (var row in rows.Skip(3).ToList())
            row.Remove();

        var cash = accounts.FirstOrDefault(x => x.AccountType == PaymentAccountTypes.Cash);
        table.Append(cashHeading);
        AppendAccount(
            table,
            cash,
            cashOpening,
            cashDetail,
            cashTotalIn,
            cashTotalOut,
            cashEnding,
            "Tiền mặt đầu kỳ",
            "Tổng tiền thu vào trong kỳ",
            "Tổng tiền chi ra trong kỳ",
            "Tiền mặt tồn cuối kỳ");

        var banks = accounts
            .Where(x => x.AccountType == PaymentAccountTypes.Bank)
            .ToList();
        if (banks.Count > 0)
            table.Append(bankGroupHeading);
        foreach (var bank in banks)
        {
            var heading = Clone(bankHeading);
            FillRow(heading, "", "", bank.DisplayName, "", "");
            table.Append(heading);
            AppendAccount(
                table,
                bank,
                bankOpening,
                bankDetail,
                bankTotalIn,
                bankTotalOut,
                bankEnding,
                "Tiền gửi đầu kỳ",
                "Tổng gửi vào trong kỳ",
                "Tổng tiền rút ra trong kỳ",
                "Tiền gửi cuối kỳ");
        }
    }

    private static void AppendAccount(
        Table table,
        S2eAccountSection? account,
        TableRow openingTemplate,
        TableRow detailTemplate,
        TableRow totalInTemplate,
        TableRow totalOutTemplate,
        TableRow endingTemplate,
        string openingLabel,
        string totalInLabel,
        string totalOutLabel,
        string endingLabel)
    {
        var opening = Clone(openingTemplate);
        FillRow(opening, "", "", openingLabel, Money(account?.OpeningBalance ?? 0m), "");
        table.Append(opening);

        foreach (var entry in account?.Entries ?? [])
        {
            var row = Clone(detailTemplate);
            FillRow(
                row,
                entry.DocumentNumber,
                entry.MovementDate.ToString("dd/MM/yyyy"),
                entry.Description,
                Money(entry.AmountIn),
                Money(entry.AmountOut));
            table.Append(row);
        }

        var totalIn = Clone(totalInTemplate);
        FillRow(totalIn, "", "", totalInLabel, Money(account?.TotalIn ?? 0m), "");
        table.Append(totalIn);
        var totalOut = Clone(totalOutTemplate);
        FillRow(totalOut, "", "", totalOutLabel, "", Money(account?.TotalOut ?? 0m));
        table.Append(totalOut);
        var ending = Clone(endingTemplate);
        FillRow(ending, "", "", endingLabel, Money(account?.EndingBalance ?? 0m), "");
        table.Append(ending);
    }

    private static TableRow Clone(TableRow row) =>
        (TableRow)row.CloneNode(true);

    private static void FillRow(TableRow row, params string[] values)
    {
        var cells = row.Elements<TableCell>().ToList();
        if (cells.Count != values.Length)
            throw new InvalidOperationException("S2e data row does not have 5 cells.");
        for (var index = 0; index < cells.Count; index++)
        {
            // Col 0, 1: Doc num, date -> Center
            // Col 2: Description -> Left
            // Col 3, 4: Amount In, Amount Out -> Right
            JustificationValues alignment = index switch
            {
                0 or 1 => JustificationValues.Center,
                2 => JustificationValues.Left,
                _ => JustificationValues.Right
            };
            SetCellLines(cells[index], values[index], alignment, "20");
        }
    }

    private static void SetCellLines(TableCell cell, params string[] lines)
    {
        SetCellLines(cell, (JustificationValues?)null, "20", lines);
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

    private static void SetCellLines(TableCell cell, string line, JustificationValues? alignment = null, string fontSize = "20")
    {
        SetCellLines(cell, alignment, fontSize, new[] { line });
    }

    private static void SetParagraphLines(Paragraph paragraph, params string[] lines)
    {
        SetParagraphLines(paragraph, null, "20", lines);
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

        foreach (var run in paragraph.Elements<Run>().ToList())
            run.Remove();
        var replacement = new Run();
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
}
