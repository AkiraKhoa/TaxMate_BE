using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TaxMate.Model.Documents.Tax;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Infrastructure.Documents.Tax;

public sealed class OpenXmlTaxDeclarationDocumentGenerator
    : ITaxDeclarationDocumentGenerator
{
    private const int SectionATableIndex = 2;
    private const int SectionDTableIndex = 5;

    private readonly string _templatePath;

    public OpenXmlTaxDeclarationDocumentGenerator()
    {
        _templatePath = Path.Combine(
            AppContext.BaseDirectory,
            "Templates",
            "Tax",
            "2026",
            "mau-01-cnkd.docx");
    }

    public async Task<TaxDeclarationGeneratedFile> GenerateAsync(
        Form01Cnkd2026Model model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!File.Exists(_templatePath))
        {
            throw new FileNotFoundException(
                "Template 01/CNKD 2026 not found.",
                _templatePath);
        }

        await using var output = new MemoryStream();

        await using (var template = File.OpenRead(_templatePath))
        {
            await template.CopyToAsync(output, cancellationToken);
        }

        output.Position = 0;

        using (var document = WordprocessingDocument.Open(output, true))
        {
            var mainPart = document.MainDocumentPart
                ?? throw new InvalidOperationException(
                    "The DOCX template does not contain a main document part.");

            var body = mainPart.Document.Body
                ?? throw new InvalidOperationException(
                    "The DOCX template does not contain a document body.");

            FillTaxMethod(body, model);
            FillPeriodAndDeclarationType(body, model);
            FillGeneralInformation(body, model);
            FillSectionA(body, model);

            FillRemainingPitDeduction(body, model);

            FillSectionD(body, model);
            FillDeclarationDate(body, model);

            mainPart.Document.Save();
        }

        return new TaxDeclarationGeneratedFile
        {
            Content = output.ToArray(),
            FileName = BuildFileName(model),
            ContentType =
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }

    private static void FillTaxMethod(Body body, Form01Cnkd2026Model model)
    {
        // For TaxMate MVP we currently support the first method:
        // PIT calculated on taxable revenue.
        if (string.Equals(
                model.TaxMethod,
                "RevenueBased",
                StringComparison.OrdinalIgnoreCase))
        {
            SetCheckboxParagraph(
                body,
                "Hộ kinh doanh, cá nhân kinh doanh thuộc đối tượng nộp thuế TNCN trên doanh thu tính thuế",
                isChecked: true);
        }

        if (string.Equals(
                model.TaxMethod,
                "IncomeBased",
                StringComparison.OrdinalIgnoreCase))
        {
            SetCheckboxParagraph(
                body,
                "Hộ kinh doanh, cá nhân kinh doanh thuộc đối tượng nộp thuế TNCN trên thu nhập tính thuế",
                isChecked: true);
        }
    }

    private static void FillPeriodAndDeclarationType(
        Body body,
        Form01Cnkd2026Model model)
    {
        var periodTable = FindTableContaining(body, "[01] Kỳ tính thuế:")
            ?? throw new InvalidOperationException(
                "Could not locate the [01] tax-period table.");

        var rows = periodTable.Elements<TableRow>().ToList();

        if (rows.Count < 2)
        {
            throw new InvalidOperationException(
                "The [01]-[03] table structure is invalid.");
        }

        var firstRowCells = rows[0].Elements<TableCell>().ToList();
        var secondRowCells = rows[1].Elements<TableCell>().ToList();

        if (firstRowCells.Count >= 2)
        {
            if (model.PeriodType == "Quarterly")
            {
                ReplaceParagraphContaining(
                    periodTable,
                    "[01b]",
                    $"[01b] Quý {model.Quarter} năm {model.Year}");
            }
            
            if (model.PeriodType == "Monthly")
            {
                ReplaceParagraphContaining(
                    periodTable,
                    "[01a]",
                    $"[01a] Tháng {model.Month} năm {model.Year}");
            }
        }

        if (secondRowCells.Count >= 2)
        {
            SetCellText(
                secondRowCells[0],
                model.IsInitialDeclaration
                    ? "[02] Lần đầu: ☒"
                    : "[02] Lần đầu: ☐");

            SetCellText(
                secondRowCells[1],
                model.IsInitialDeclaration
                    ? "[03] Bổ sung lần thứ: .... ☐"
                    : $"[03] Bổ sung lần thứ: {model.SupplementNumber ?? 1} ☒");
        }
    }

    private static void FillGeneralInformation(
        Body body,
        Form01Cnkd2026Model model)
    {
        SetParagraphValue(body, "[04]", "Người nộp thuế", model.TaxpayerName);
        SetParagraphValue(body, "[05]", "Mã số thuế", model.TaxCode);

        SetParagraphValue(
            body,
            "[06]",
            "Tổ chức/cá nhân khai, nộp thuế thay theo ủy quyền (nếu có)",
            model.AuthorizedDeclarerName);

        SetParagraphValue(
            body,
            "[06.1]",
            "Mã số thuế",
            model.AuthorizedDeclarerTaxCode);

        SetAuthorizationParagraph(body, model);

        SetParagraphValue(
            body,
            "[07]",
            "Tên đại lý thuế (nếu có)",
            model.TaxAgentName);

        SetParagraphValue(
            body,
            "[07.1]",
            "Mã số thuế",
            model.TaxAgentTaxCode);
    }

    private static void FillSectionA(
        Body body,
        Form01Cnkd2026Model model)
    {
        var tables = body.Elements<Table>().ToList();

        if (tables.Count <= SectionATableIndex)
        {
            throw new InvalidOperationException(
                "Could not locate section A table in the 01/CNKD template.");
        }

        var table = tables[SectionATableIndex];
        var rows = table.Elements<TableRow>().ToList();

        /*
         * Template 2026:
         *
         * row 4  = "1 Trụ sở kinh doanh"
         * row 5-10 = 1.1 ... 1.6
         * row 11 = header "2 Mã địa điểm kinh doanh 1 / Tên..."
         * row 12 = generic 2.1 data-row template
         * row 13 = continuation placeholder
         * row 14 = Section II
         *
         * Location đầu tiên trong model.Lines là anchor/trụ sở.
         * Các location còn lại được clone thành các block 2.x, 3.x...
         */
        var locationGroups =
            model.Lines
                .Where(x =>
                    string.Equals(
                        x.SectionCode,
                        "I",
                        StringComparison.OrdinalIgnoreCase))
                .GroupBy(
                    x => x.BusinessLocationCode ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (locationGroups.Count > 0)
        {
            FillHeadOfficeLines(
                rows,
                locationGroups[0]);

            if (locationGroups.Count > 1)
            {
                RebuildAdditionalLocationRows(
                    table,
                    locationGroups.Skip(1).ToList());
            }
            else
            {
                ClearAdditionalLocationTemplateRows(table);
            }
        }

        /*
         * Các section khác (nếu có) vẫn dùng mapping theo activity code.
         */
        var refreshedRows =
            table.Elements<TableRow>().ToList();

        foreach (var line in model.Lines
                     .Where(x =>
                         !string.Equals(
                             x.SectionCode,
                             "I",
                             StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.DisplayOrder))
        {
            var row =
                FindSectionARow(
                    refreshedRows,
                    line);

            if (row is null)
            {
                throw new InvalidOperationException(
                    $"Could not find section A row for section '{line.SectionCode}' " +
                    $"and activity code '{line.ActivityCode}'.");
            }

            FillSectionADataRow(
                row,
                line,
                overwriteIdentityCells: false);
        }

        refreshedRows =
            table.Elements<TableRow>().ToList();

        FillSectionASummary(
            refreshedRows,
            model.Summary);
    }

    private static void FillHeadOfficeLines(
        IReadOnlyList<TableRow> rows,
        IEnumerable<Form01Cnkd2026LineModel> lines)
    {
        foreach (var line in lines
                     .OrderBy(x => x.DisplayOrder))
        {
            var row =
                FindFixedLocationActivityRow(
                    rows,
                    locationOrdinal: 1,
                    line.ActivityCode);

            if (row is null)
            {
                throw new InvalidOperationException(
                    $"Could not find head-office activity row for activity '{line.ActivityCode}'.");
            }

            FillSectionADataRow(
                row,
                line,
                overwriteIdentityCells: false);
        }
    }

    private static void RebuildAdditionalLocationRows(
        Table table,
        IReadOnlyList<IGrouping<string, Form01Cnkd2026LineModel>> locationGroups)
    {
        var rows =
            table.Elements<TableRow>().ToList();

        if (rows.Count <= 14)
        {
            throw new InvalidOperationException(
                "Section A location template structure is invalid.");
        }

        var headerTemplate =
            (TableRow)rows[11].CloneNode(true);

        var dataTemplate =
            (TableRow)rows[12].CloneNode(true);

        var sectionIIRow =
            rows.FirstOrDefault(
                x => x.Elements<TableCell>()
                    .FirstOrDefault()?
                    .InnerText.Trim()
                    .Equals(
                        "II",
                        StringComparison.OrdinalIgnoreCase) == true)
            ?? throw new InvalidOperationException(
                "Could not locate Section II row after fixed-location section.");

        /*
         * Xóa header/data placeholder location cũ:
         * row 11, 12, 13 trong template.
         */
        foreach (var oldRow in rows
                     .Skip(11)
                     .TakeWhile(x => !ReferenceEquals(x, sectionIIRow))
                     .ToList())
        {
            oldRow.Remove();
        }

        for (var groupIndex = 0;
             groupIndex < locationGroups.Count;
             groupIndex++)
        {
            var group =
                locationGroups[groupIndex];

            var sectionOrdinal =
                groupIndex + 2;

            var locationSequence =
                groupIndex + 1;

            var firstLine =
                group.OrderBy(x => x.DisplayOrder)
                    .First();

            var headerRow =
                (TableRow)headerTemplate.CloneNode(true);

            FillLocationHeaderRow(
                headerRow,
                sectionOrdinal,
                locationSequence,
                firstLine.BusinessLocationCode,
                firstLine.BusinessLocationName);

            sectionIIRow.InsertBeforeSelf(
                headerRow);

            foreach (var line in group
                         .OrderBy(x => x.DisplayOrder))
            {
                var dataRow =
                    (TableRow)dataTemplate.CloneNode(true);

                FillAdditionalLocationDataRow(
                    dataRow,
                    sectionOrdinal,
                    line);

                sectionIIRow.InsertBeforeSelf(
                    dataRow);
            }
        }
    }

    private static void ClearAdditionalLocationTemplateRows(
        Table table)
    {
        var rows =
            table.Elements<TableRow>().ToList();

        if (rows.Count <= 14)
            return;

        var sectionIIRow =
            rows.FirstOrDefault(
                x => x.Elements<TableCell>()
                    .FirstOrDefault()?
                    .InnerText.Trim()
                    .Equals(
                        "II",
                        StringComparison.OrdinalIgnoreCase) == true);

        if (sectionIIRow is null)
            return;

        foreach (var oldRow in rows
                     .Skip(11)
                     .TakeWhile(x => !ReferenceEquals(x, sectionIIRow))
                     .ToList())
        {
            oldRow.Remove();
        }
    }

    private static void FillLocationHeaderRow(
        TableRow row,
        int sectionOrdinal,
        int locationSequence,
        string? locationCode,
        string? locationName)
    {
        var cells =
            row.Elements<TableCell>().ToList();

        if (cells.Count < 2)
        {
            throw new InvalidOperationException(
                "Location header row must contain at least 2 cells.");
        }

        SetCellText(
            cells[0],
            sectionOrdinal.ToString(
                CultureInfo.InvariantCulture));

        var paragraphs =
            cells[1].Elements<Paragraph>().ToList();

        if (paragraphs.Count >= 2)
        {
            SetParagraphText(
                paragraphs[0],
                $"Mã địa điểm kinh doanh {locationSequence}: " +
                $"{locationCode ?? string.Empty}");

            SetParagraphText(
                paragraphs[1],
                $"Tên địa điểm kinh doanh {locationSequence}: " +
                $"{locationName ?? string.Empty}");
        }
        else
        {
            SetCellText(
                cells[1],
                $"Mã địa điểm kinh doanh {locationSequence}: " +
                $"{locationCode ?? string.Empty}; " +
                $"Tên địa điểm kinh doanh {locationSequence}: " +
                $"{locationName ?? string.Empty}");
        }
    }

    private static void FillAdditionalLocationDataRow(
        TableRow row,
        int sectionOrdinal,
        Form01Cnkd2026LineModel line)
    {
        var cells =
            row.Elements<TableCell>().ToList();

        if (cells.Count < 10)
        {
            throw new InvalidOperationException(
                "Additional-location data row must contain 10 cells.");
        }

        var activityOrdinal =
            ResolveActivityOrdinal(
                line.ActivityCode);

        SetCellText(
            cells[0],
            $"{sectionOrdinal}.{activityOrdinal}");

        SetCellText(
            cells[1],
            line.ActivityName);

        SetCellText(
            cells[2],
            $"({NormalizeActivityCode(line.ActivityCode)})");

        FillSectionADataRow(
            row,
            line,
            overwriteIdentityCells: false);
    }

    private static void FillSectionADataRow(
        TableRow row,
        Form01Cnkd2026LineModel line,
        bool overwriteIdentityCells)
    {
        var cells =
            row.Elements<TableCell>().ToList();

        if (cells.Count < 10)
        {
            throw new InvalidOperationException(
                "A section A data row must contain 10 cells.");
        }

        if (overwriteIdentityCells)
        {
            SetCellText(
                cells[1],
                line.ActivityName);

            SetCellText(
                cells[2],
                $"({NormalizeActivityCode(line.ActivityCode)})");
        }

        // Actual 2026 Word structure:
        // 0 STT [08]
        // 1 activity [09]
        // 2 activity code [10]
        // 3 total revenue [11]
        // 4 VAT non-taxable revenue [12]
        // 5 VAT 0% revenue [13]
        // 6 VAT payable [14]
        // 7 PIT taxable revenue [15]
        // 8 PIT deductible revenue [16]
        // 9 PIT payable [17]
        SetMoneyCell(cells[3], line.TotalRevenue);
        SetMoneyCell(cells[4], line.VatNonTaxableRevenue);
        SetMoneyCell(cells[5], line.ZeroRatedVatRevenue);
        SetMoneyCell(cells[6], line.VatTaxAmount);
        SetMoneyCell(cells[7], line.PersonalIncomeTaxableRevenue);
        SetMoneyCell(cells[8], line.PersonalIncomeTaxDeductibleRevenue);
        SetMoneyCell(cells[9], line.PersonalIncomeTaxAmount);
    }

    private static TableRow? FindFixedLocationActivityRow(
        IReadOnlyList<TableRow> rows,
        int locationOrdinal,
        string activityCode)
    {
        if (locationOrdinal != 1)
        {
            return null;
        }

        var marker =
            $"({NormalizeActivityCode(activityCode)})";

        /*
         * Trụ sở trong template nằm tại row 5-10.
         */
        for (var i = 5;
             i <= 10 && i < rows.Count;
             i++)
        {
            var cells =
                rows[i].Elements<TableCell>().ToList();

            if (cells.Count < 3)
                continue;

            if (NormalizeText(cells[2].InnerText) ==
                NormalizeText(marker))
            {
                return rows[i];
            }
        }

        return null;
    }

    private static int ResolveActivityOrdinal(
        string activityCode)
    {
        return NormalizeActivityCode(
            activityCode) switch
        {
            "a" => 1,
            "b" => 2,
            "c" => 3,
            "d" => 4,
            "đ" => 5,
            "e" => 6,

            _ => throw new InvalidOperationException(
                $"Unsupported Section A activity code: {activityCode}.")
        };
    }

    private static void FillSectionASummary(
        IReadOnlyList<TableRow> rows,
        Form01Cnkd2026SummaryModel summary)
    {
        var totalRow = FindRowByMarker(rows, "[18]");
        var exemptionRow = FindRowByMarker(rows, "[19]");
        var payableRow = FindRowByMarker(rows, "[20]");

        if (totalRow is not null)
        {
            var cells = totalRow.Elements<TableCell>().ToList();

            if (cells.Count >= 10)
            {
                SetMoneyCell(cells[3], summary.TotalRevenue);
                SetMoneyCell(cells[4], summary.TotalVatNonTaxableRevenue);
                SetMoneyCell(cells[5], summary.TotalZeroRatedVatRevenue);
                SetMoneyCell(cells[6], summary.TotalVatTaxAmount);
                SetMoneyCell(cells[7], summary.TotalPersonalIncomeTaxableRevenue);
                SetMoneyCell(cells[8], summary.TotalPersonalIncomeTaxDeductibleRevenue);
                SetMoneyCell(cells[9], summary.TotalPersonalIncomeTaxAmount);
            }
        }

        if (exemptionRow is not null)
        {
            var cells = exemptionRow.Elements<TableCell>().ToList();

            if (cells.Count >= 10)
            {
                // Exemption is tax amount, so only tax-amount columns are populated.
                SetMoneyCell(cells[6], summary.VatExemptionAmount);
                SetMoneyCell(cells[9], summary.PersonalIncomeTaxExemptionAmount);
            }
        }

        if (payableRow is not null)
        {
            var cells = payableRow.Elements<TableCell>().ToList();

            if (cells.Count >= 10)
            {
                // Remaining payable amounts, again only in the VAT/PIT tax columns.
                SetMoneyCell(cells[6], summary.VatPayableAmount);
                SetMoneyCell(cells[9], summary.PersonalIncomeTaxPayableAmount);
            }
        }
    }

    private static void FillSectionD(
        Body body,
        Form01Cnkd2026Model model)
    {
        if (model.PaymentLines.Count == 0)
            return;

        var tables = body.Elements<Table>().ToList();

        if (tables.Count <= SectionDTableIndex)
        {
            throw new InvalidOperationException(
                "Could not locate section D table in the 01/CNKD template.");
        }

        var table = tables[SectionDTableIndex];
        var rows = table.Elements<TableRow>().ToList();

        // Row 0 = headers; row 1 = [37]...[46]; row 2 = first data row;
        // row 3 = continuation placeholder; row 4 = total.
        if (rows.Count < 5)
        {
            throw new InvalidOperationException(
                "Section D table structure is invalid.");
        }

        var templateDataRow = rows[2];

        // Remove any previously generated extra rows between template row and total row.
        for (var i = rows.Count - 2; i > 2; i--)
        {
            rows[i].Remove();
        }

        TableRow? previousRow = null;

        for (var index = 0; index < model.PaymentLines.Count; index++)
        {
            var line = model.PaymentLines[index];

            TableRow row;

            if (index == 0)
            {
                row = templateDataRow;
            }
            else
            {
                row = (TableRow)templateDataRow.CloneNode(true);
                previousRow!.InsertAfterSelf(row);
            }

            FillPaymentRow(row, index + 1, line);
            previousRow = row;
        }

        var refreshedRows = table.Elements<TableRow>().ToList();
        var totalRow = refreshedRows.Last();
        var totalCells = totalRow.Elements<TableCell>().ToList();

        if (totalCells.Count >= 4)
        {
            SetMoneyCell(
                totalCells[3],
                model.PaymentLines.Sum(x => x.Amount));
        }
    }

    private static void FillPaymentRow(
        TableRow row,
        int number,
        Form01Cnkd2026PaymentLineModel line)
    {
        var cells = row.Elements<TableCell>().ToList();

        if (cells.Count < 10)
            return;

        SetCellText(cells[0], number.ToString(CultureInfo.InvariantCulture));
        SetCellText(cells[1], line.BusinessLocationCode ?? string.Empty);
        SetCellText(cells[2], line.StateBudgetContent);
        SetMoneyCell(cells[3], line.Amount);
        SetCellText(cells[4], line.ChapterCode ?? string.Empty);
        SetCellText(cells[5], line.SubsectionCode ?? string.Empty);
        SetCellText(cells[6], line.AdministrativeAreaCode ?? string.Empty);
        SetCellText(cells[7], line.CollectingAuthority ?? string.Empty);
        SetCellText(cells[8], line.TaxAuthority ?? string.Empty);
        SetCellText(
            cells[9],
            line.DueDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            ?? string.Empty);
    }

    private static TableRow? FindSectionARow(
        IReadOnlyList<TableRow> rows,
        Form01Cnkd2026LineModel line)
    {
        var activityMarker = $"({NormalizeActivityCode(line.ActivityCode)})";

        var startIndex = string.Equals(
            line.SectionCode,
            "II",
            StringComparison.OrdinalIgnoreCase)
            ? 14
            : 3;

        var endIndex = string.Equals(
            line.SectionCode,
            "II",
            StringComparison.OrdinalIgnoreCase)
            ? 21
            : 10;

        for (var i = startIndex; i <= endIndex && i < rows.Count; i++)
        {
            var cells = rows[i].Elements<TableCell>().ToList();

            if (cells.Count < 3)
                continue;

            if (NormalizeText(cells[2].InnerText) ==
                NormalizeText(activityMarker))
            {
                return rows[i];
            }
        }

        return null;
    }

    private static TableRow? FindRowByMarker(
        IEnumerable<TableRow> rows,
        string marker)
    {
        return rows.FirstOrDefault(
            row => row.Elements<TableCell>()
                .Any(cell => cell.InnerText.Contains(
                    marker,
                    StringComparison.OrdinalIgnoreCase)));
    }

    private static Table? FindTableContaining(
        Body body,
        string marker)
    {
        return body.Elements<Table>()
            .FirstOrDefault(
                table => table.InnerText.Contains(
                    marker,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static void SetCheckboxParagraph(
        Body body,
        string containsText,
        bool isChecked)
    {
        var paragraph = body.Elements<Paragraph>()
            .FirstOrDefault(
                x => NormalizeText(x.InnerText)
                    .Contains(
                        NormalizeText(containsText),
                        StringComparison.OrdinalIgnoreCase));

        if (paragraph is null)
            return;

        var original = paragraph.InnerText.Trim();
        original = original.TrimStart('□', '☐', '☒', ' ');

        SetParagraphText(
            paragraph,
            $"{(isChecked ? "☒" : "☐")} {original}");
    }

    private static void SetParagraphValue(
        Body body,
        string marker,
        string label,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var paragraph = body.Elements<Paragraph>()
            .FirstOrDefault(
                x => x.InnerText.Contains(
                    marker,
                    StringComparison.OrdinalIgnoreCase));

        if (paragraph is null)
            return;

        SetParagraphText(
            paragraph,
            $"{marker} {label}: {value.Trim()}");
    }

    private static void SetAuthorizationParagraph(
        Body body,
        Form01Cnkd2026Model model)
    {
        if (string.IsNullOrWhiteSpace(model.AuthorizationNumber) &&
            model.AuthorizationDate is null)
        {
            return;
        }

        var paragraph = body.Elements<Paragraph>()
            .FirstOrDefault(
                x => x.InnerText.Contains(
                    "[06.2]",
                    StringComparison.OrdinalIgnoreCase));

        if (paragraph is null)
            return;

        var dateText = model.AuthorizationDate.HasValue
            ? $" ngày {model.AuthorizationDate.Value:dd} " +
              $"tháng {model.AuthorizationDate.Value:MM} " +
              $"năm {model.AuthorizationDate.Value:yyyy}"
            : string.Empty;

        SetParagraphText(
            paragraph,
            $"[06.2] Văn bản ủy quyền (nếu có): Số " +
            $"{model.AuthorizationNumber ?? string.Empty}{dateText}");
    }

    private static void SetMoneyCell(
        TableCell cell,
        decimal value)
    {
        SetCellText(
            cell,
            value == 0m
                ? string.Empty
                : value.ToString("#,##0.##", CultureInfo.GetCultureInfo("vi-VN")));
    }

    private static void SetCellText(
        TableCell cell,
        string value)
    {
        var paragraph = cell.Elements<Paragraph>().FirstOrDefault();

        if (paragraph is null)
        {
            paragraph = new Paragraph();
            cell.Append(paragraph);
        }

        SetParagraphText(paragraph, value);
    }

    private static void SetParagraphText(
        Paragraph paragraph,
        string value)
    {
        var paragraphProperties =
            paragraph.ParagraphProperties?.CloneNode(true) as ParagraphProperties;

        var firstRunProperties =
            paragraph.Descendants<RunProperties>()
                .FirstOrDefault()?
                .CloneNode(true) as RunProperties;

        paragraph.RemoveAllChildren();

        if (paragraphProperties is not null)
            paragraph.Append(paragraphProperties);

        var run = new Run();

        if (firstRunProperties is not null)
            run.Append(firstRunProperties);

        run.Append(
            new Text(value)
            {
                Space = SpaceProcessingModeValues.Preserve
            });

        paragraph.Append(run);
    }

    private static string NormalizeActivityCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var code = value
            .Trim()
            .Trim('(', ')')
            .ToLowerInvariant();

        return code switch
        {
            "08a" => "a",
            "08b" => "b",
            "08c" => "c",
            "08d" => "d",
            "08đ" => "đ",
            "08e" => "e",

            "a" => "a",
            "b" => "b",
            "c" => "c",
            "d" => "d",
            "đ" => "đ",
            "e" => "e",

            _ => code
        };
    }

    private static string NormalizeText(string value)
    {
        return string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
    }

    private static string BuildFileName(
        Form01Cnkd2026Model model)
    {
        var period = model.PeriodType switch
        {
            "Quarterly" => $"Q{model.Quarter}-{model.Year}",
            "Monthly" => $"M{model.Month}-{model.Year}",
            _ => model.Year.ToString(CultureInfo.InvariantCulture)
        };

        var safeTaxCode = string.Concat(
            model.TaxCode.Where(char.IsLetterOrDigit));

        return $"01-CNKD-{safeTaxCode}-{period}.docx";
    }
    
    private static void ReplaceParagraphContaining(
        OpenXmlElement root,
        string marker,
        string replacement)
    {
        var paragraph = root
            .Descendants<Paragraph>()
            .FirstOrDefault(x =>
                x.InnerText.Contains(
                    marker,
                    StringComparison.OrdinalIgnoreCase));

        if (paragraph is null)
        {
            return;
        }

        SetParagraphText(
            paragraph,
            replacement);
    }
    
    private static void FillDeclarationDate(
        Body body,
        Form01Cnkd2026Model model)
    {
        var paragraph = body
            .Descendants<Paragraph>()
            .FirstOrDefault(x =>
                x.InnerText.Contains(
                    "ngày ... tháng ... năm",
                    StringComparison.OrdinalIgnoreCase));

        if (paragraph is null)
        {
            return;
        }

        var date = model.DeclarationDate;

        SetParagraphText(
            paragraph,
            $"Ngày {date.Day:00} tháng {date.Month:00} năm {date.Year}");
    }
    
    private static void FillRemainingPitDeduction(
        Body body,
        Form01Cnkd2026Model model)
    {
        const string marker =
            "Bạn còn được tiếp tục trừ";

        var paragraph = body
            .Descendants<Paragraph>()
            .FirstOrDefault(x =>
                NormalizeText(x.InnerText)
                    .Contains(
                        NormalizeText(marker),
                        StringComparison.OrdinalIgnoreCase));

        if (paragraph is null)
        {
            throw new InvalidOperationException(
                "Could not find remaining PIT deduction paragraph " +
                "in the 01/CNKD template.");
        }

        var value =
            model.RemainingPitDeduction.ToString(
                "#,##0.##",
                CultureInfo.GetCultureInfo("vi-VN"));

        SetParagraphText(
            paragraph,
            $"Bạn còn được tiếp tục trừ {value} đồng " +
            "vào doanh thu tính thuế TNCN của kỳ tiếp theo");
    }
}
