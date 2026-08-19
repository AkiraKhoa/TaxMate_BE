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

            /*
             * Final presentation pass:
             * - one font family across the entire exported form;
             * - compact paragraph spacing inside tables;
             * - optimized Section A/D numeric and administrative columns.
             *
             * This pass intentionally preserves bold/italic and existing
             * template hierarchy while normalizing the visual system.
             */
            ApplyUnifiedDocumentFormatting(body);
            OptimizeSectionATableLayout(body);
            OptimizeSectionDTableLayout(body);

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
                    x => !string.IsNullOrWhiteSpace(x.BusinessLocationCode)
                        ? x.BusinessLocationCode!
                        : $"__NO_CODE__::{x.BusinessLocationName ?? string.Empty}",
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
                    table,
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
        Table sectionATable,
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

        // Keep the official activity wording/code from the 01/CNKD template.
        // Internal names such as "FNB" or "Dịch vụ" must not replace [09].
        CopyOfficialActivityIdentity(
            sectionATable,
            row,
            sectionOrdinal,
            line.ActivityCode);

        FillSectionADataRow(
            row,
            line,
            overwriteIdentityCells: false);
    }

    private static void CopyOfficialActivityIdentity(
        Table sectionATable,
        TableRow targetRow,
        int sectionOrdinal,
        string activityCode)
    {
        var sourceRow = FindFixedLocationActivityRow(
            sectionATable.Elements<TableRow>().ToList(),
            locationOrdinal: 1,
            activityCode)
            ?? throw new InvalidOperationException(
                $"Could not locate official template activity row for '{activityCode}'.");

        var sourceCells = sourceRow.Elements<TableCell>().ToList();
        var targetCells = targetRow.Elements<TableCell>().ToList();

        if (sourceCells.Count < 3 || targetCells.Count < 3)
        {
            throw new InvalidOperationException(
                "Section A activity row structure is invalid.");
        }

        SetCellText(
            targetCells[0],
            $"{sectionOrdinal}.{ResolveActivityOrdinal(activityCode)}");

        CopyCellParagraphContent(sourceCells[1], targetCells[1]);
        CopyCellParagraphContent(sourceCells[2], targetCells[2]);
    }

    private static void CopyCellParagraphContent(
        TableCell source,
        TableCell target)
    {
        var targetProperties =
            target.TableCellProperties?.CloneNode(true) as TableCellProperties;

        target.RemoveAllChildren();

        if (targetProperties is not null)
        {
            target.Append(targetProperties);
        }

        foreach (var paragraph in source.Elements<Paragraph>())
        {
            target.Append(paragraph.CloneNode(true));
        }

        if (!target.Elements<Paragraph>().Any())
        {
            target.Append(new Paragraph());
        }
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
                : value.ToString(
                    "#,##0.##",
                    CultureInfo.GetCultureInfo("vi-VN")));

        EnsureNoWrap(cell);
        EnsureCompactMoneyFont(cell);
    }

    private static void EnsureNoWrap(TableCell cell)
    {
        var properties = cell.GetFirstChild<TableCellProperties>();

        if (properties is null)
        {
            properties = new TableCellProperties();
            cell.PrependChild(properties);
        }

        if (properties.GetFirstChild<NoWrap>() is null)
        {
            properties.Append(new NoWrap());
        }

        /*
         * tcFitText keeps long monetary values on one visual line by
         * horizontally fitting the run inside the existing template cell.
         * This preserves the official table widths instead of rebuilding
         * the template grid.
         */
        if (properties.GetFirstChild<TableCellFitText>() is null)
        {
            properties.Append(
                new TableCellFitText
                {
                    Val = OnOffOnlyValues.On
                });
        }

        foreach (var paragraph in cell.Elements<Paragraph>())
        {
            var paragraphProperties =
                paragraph.ParagraphProperties;

            if (paragraphProperties is null)
            {
                paragraphProperties = new ParagraphProperties();
                paragraph.PrependChild(paragraphProperties);
            }

            var justification =
                paragraphProperties.GetFirstChild<Justification>();

            if (justification is null)
            {
                paragraphProperties.Append(
                    new Justification
                    {
                        Val = JustificationValues.Right
                    });
            }
            else
            {
                justification.Val =
                    JustificationValues.Right;
            }
        }
    }

    private static void EnsureCompactMoneyFont(TableCell cell)
    {
        const string fontSize = "17"; // 8.5 pt, numeric cells only.

        foreach (var run in cell.Descendants<Run>())
        {
            var properties = run.RunProperties;

            if (properties is null)
            {
                properties = new RunProperties();
                run.PrependChild(properties);
            }

            var size = properties.GetFirstChild<FontSize>();
            if (size is null)
                properties.Append(new FontSize { Val = fontSize });
            else
                size.Val = fontSize;

            var complexSize = properties.GetFirstChild<FontSizeComplexScript>();
            if (complexSize is null)
                properties.Append(new FontSizeComplexScript { Val = fontSize });
            else
                complexSize.Val = fontSize;
        }
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

    private static void ApplyUnifiedDocumentFormatting(
        Body body)
    {
        const string fontFamily = "Times New Roman";

        foreach (var run in body.Descendants<Run>())
        {
            var properties = run.RunProperties;

            if (properties is null)
            {
                properties = new RunProperties();
                run.PrependChild(properties);
            }

            var fonts =
                properties.GetFirstChild<RunFonts>();

            if (fonts is null)
            {
                fonts = new RunFonts();
                properties.PrependChild(fonts);
            }

            fonts.Ascii = fontFamily;
            fonts.HighAnsi = fontFamily;
            fonts.EastAsia = fontFamily;
            fonts.ComplexScript = fontFamily;
        }

        /*
         * Table content is intentionally compact. Outside tables we preserve
         * the template's point sizes so the official title/header hierarchy
         * remains intact while still using the same font family.
         */
        foreach (var table in body.Elements<Table>())
        {
            foreach (var paragraph in table.Descendants<Paragraph>())
            {
                EnsureCompactParagraphSpacing(paragraph);
            }

            foreach (var run in table.Descendants<Run>())
            {
                EnsureRunFontSize(
                    run,
                    halfPoints: "18"); // 9 pt
            }
        }

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            EnsureBodyParagraphSpacing(paragraph);
        }
    }

    private static void EnsureCompactParagraphSpacing(
        Paragraph paragraph)
    {
        var properties =
            paragraph.ParagraphProperties;

        if (properties is null)
        {
            properties = new ParagraphProperties();
            paragraph.PrependChild(properties);
        }

        var spacing =
            properties.GetFirstChild<SpacingBetweenLines>();

        if (spacing is null)
        {
            spacing = new SpacingBetweenLines();
            properties.Append(spacing);
        }

        spacing.Before = "0";
        spacing.After = "0";
        spacing.Line = "220";
        spacing.LineRule = LineSpacingRuleValues.Auto;
    }

    private static void EnsureBodyParagraphSpacing(
        Paragraph paragraph)
    {
        var properties =
            paragraph.ParagraphProperties;

        if (properties is null)
        {
            properties = new ParagraphProperties();
            paragraph.PrependChild(properties);
        }

        var spacing =
            properties.GetFirstChild<SpacingBetweenLines>();

        if (spacing is null)
        {
            spacing = new SpacingBetweenLines();
            properties.Append(spacing);
        }

        /*
         * Outside tables keep a little breathing room, but remove the
         * oversized spacing that makes the form unnecessarily long.
         */
        spacing.Before ??= "0";
        spacing.After ??= "40";
        spacing.Line ??= "240";
        spacing.LineRule ??= LineSpacingRuleValues.Auto;
    }

    private static void EnsureRunFontSize(
        Run run,
        string halfPoints)
    {
        var properties =
            run.RunProperties;

        if (properties is null)
        {
            properties = new RunProperties();
            run.PrependChild(properties);
        }

        var size =
            properties.GetFirstChild<FontSize>();

        if (size is null)
        {
            properties.Append(
                new FontSize
                {
                    Val = halfPoints
                });
        }
        else
        {
            size.Val = halfPoints;
        }

        var complexSize =
            properties.GetFirstChild<FontSizeComplexScript>();

        if (complexSize is null)
        {
            properties.Append(
                new FontSizeComplexScript
                {
                    Val = halfPoints
                });
        }
        else
        {
            complexSize.Val = halfPoints;
        }
    }

    private static void OptimizeSectionATableLayout(
        Body body)
    {
        var tables =
            body.Elements<Table>().ToList();

        if (tables.Count <= SectionATableIndex)
            return;

        var table =
            tables[SectionATableIndex];

        SetTableFixedLayout(table);

        /*
         * Keep the official Section A proportions but give monetary columns
         * enough effective room by making all numeric content compact and
         * vertically centered. FitText/NoWrap added by SetMoneyCell remains.
         */
        foreach (var row in table.Elements<TableRow>())
        {
            var cells =
                row.Elements<TableCell>().ToList();

            if (cells.Count < 10)
                continue;

            for (var i = 3; i <= 9; i++)
            {
                SetCellVerticalCenter(cells[i]);

                foreach (var paragraph in cells[i].Elements<Paragraph>())
                {
                    SetParagraphRightAligned(paragraph);
                }
            }
        }
    }

    private static void OptimizeSectionDTableLayout(
        Body body)
    {
        var tables =
            body.Elements<Table>().ToList();

        if (tables.Count <= SectionDTableIndex)
            return;

        var table =
            tables[SectionDTableIndex];

        SetTableFixedLayout(table);

        /*
         * A4-friendly fixed grid, total = 9,300 twips.
         * Priority is readability of:
         * - business location code,
         * - NSNN description,
         * - collecting authority / tax authority,
         * while keeping code columns compact.
         */
        var widths = new[]
        {
            430,  // STT
            900,  // Business location code
            1480, // NSNN content
            850,  // Amount
            700,  // Chapter
            700,  // Subsection
            760,  // Administrative area
            1120, // Collecting authority
            1120, // Tax authority
            1240  // Due date
        };

        SetTableGridWidths(
            table,
            widths);

        foreach (var row in table.Elements<TableRow>())
        {
            var cells =
                row.Elements<TableCell>().ToList();

            if (cells.Count < 10)
                continue;

            for (var i = 0; i < 10; i++)
            {
                SetCellWidth(
                    cells[i],
                    widths[i]);

                SetCellVerticalCenter(
                    cells[i]);

                SetCellMargins(
                    cells[i],
                    top: 45,
                    right: 55,
                    bottom: 45,
                    left: 55);
            }

            /*
             * STT, location code, amount, budget codes and due date should
             * stay compact and visually stable.
             */
            foreach (var index in new[] { 0, 1, 3, 4, 5, 6, 9 })
            {
                EnsureCellNoWrapOnly(
                    cells[index]);
            }

            foreach (var paragraph in cells[0].Elements<Paragraph>())
                SetParagraphCenterAligned(paragraph);

            foreach (var paragraph in cells[1].Elements<Paragraph>())
                SetParagraphCenterAligned(paragraph);

            foreach (var paragraph in cells[3].Elements<Paragraph>())
                SetParagraphRightAligned(paragraph);

            foreach (var index in new[] { 4, 5, 6, 9 })
            {
                foreach (var paragraph in cells[index].Elements<Paragraph>())
                    SetParagraphCenterAligned(paragraph);
            }

            /*
             * Slightly smaller text only in the dense payment table.
             */
            foreach (var run in row.Descendants<Run>())
            {
                EnsureRunFontSize(
                    run,
                    halfPoints: "17"); // 8.5 pt
            }
        }
    }

    private static void SetTableFixedLayout(
        Table table)
    {
        var properties =
            table.GetFirstChild<TableProperties>();

        if (properties is null)
        {
            properties = new TableProperties();
            table.PrependChild(properties);
        }

        var layout =
            properties.GetFirstChild<TableLayout>();

        if (layout is null)
        {
            properties.Append(
                new TableLayout
                {
                    Type = TableLayoutValues.Fixed
                });
        }
        else
        {
            layout.Type =
                TableLayoutValues.Fixed;
        }
    }

    private static void SetTableGridWidths(
        Table table,
        IReadOnlyList<int> widths)
    {
        var grid =
            table.GetFirstChild<TableGrid>();

        if (grid is null)
        {
            grid = new TableGrid();
            var properties =
                table.GetFirstChild<TableProperties>();

            if (properties is not null)
            {
                properties.InsertAfterSelf(grid);
            }
            else
            {
                table.PrependChild(grid);
            }
        }

        grid.RemoveAllChildren<GridColumn>();

        foreach (var width in widths)
        {
            grid.Append(
                new GridColumn
                {
                    Width = width.ToString(
                        CultureInfo.InvariantCulture)
                });
        }
    }

    private static void SetCellWidth(
        TableCell cell,
        int width)
    {
        var properties =
            cell.GetFirstChild<TableCellProperties>();

        if (properties is null)
        {
            properties = new TableCellProperties();
            cell.PrependChild(properties);
        }

        var cellWidth =
            properties.GetFirstChild<TableCellWidth>();

        if (cellWidth is null)
        {
            properties.Append(
                new TableCellWidth
                {
                    Type = TableWidthUnitValues.Dxa,
                    Width = width.ToString(
                        CultureInfo.InvariantCulture)
                });
        }
        else
        {
            cellWidth.Type =
                TableWidthUnitValues.Dxa;

            cellWidth.Width =
                width.ToString(
                    CultureInfo.InvariantCulture);
        }
    }

    private static void SetCellVerticalCenter(
        TableCell cell)
    {
        var properties =
            cell.GetFirstChild<TableCellProperties>();

        if (properties is null)
        {
            properties = new TableCellProperties();
            cell.PrependChild(properties);
        }

        var vertical =
            properties.GetFirstChild<TableCellVerticalAlignment>();

        if (vertical is null)
        {
            properties.Append(
                new TableCellVerticalAlignment
                {
                    Val = TableVerticalAlignmentValues.Center
                });
        }
        else
        {
            vertical.Val =
                TableVerticalAlignmentValues.Center;
        }
    }

    private static void SetCellMargins(
        TableCell cell,
        int top,
        int right,
        int bottom,
        int left)
    {
        var properties =
            cell.GetFirstChild<TableCellProperties>();

        if (properties is null)
        {
            properties = new TableCellProperties();
            cell.PrependChild(properties);
        }

        var margins =
            properties.GetFirstChild<TableCellMargin>();

        if (margins is null)
        {
            margins = new TableCellMargin();
            properties.Append(margins);
        }

        margins.TopMargin = new TopMargin
        {
            Width = top.ToString(
                CultureInfo.InvariantCulture),
            Type = TableWidthUnitValues.Dxa
        };

        margins.RightMargin = new RightMargin
        {
            Width = right.ToString(
                CultureInfo.InvariantCulture),
            Type = TableWidthUnitValues.Dxa
        };

        margins.BottomMargin = new BottomMargin
        {
            Width = bottom.ToString(
                CultureInfo.InvariantCulture),
            Type = TableWidthUnitValues.Dxa
        };

        margins.LeftMargin = new LeftMargin
        {
            Width = left.ToString(
                CultureInfo.InvariantCulture),
            Type = TableWidthUnitValues.Dxa
        };
    }

    private static void EnsureCellNoWrapOnly(
        TableCell cell)
    {
        var properties =
            cell.GetFirstChild<TableCellProperties>();

        if (properties is null)
        {
            properties = new TableCellProperties();
            cell.PrependChild(properties);
        }

        if (properties.GetFirstChild<NoWrap>() is null)
        {
            properties.Append(
                new NoWrap());
        }
    }

    private static void SetParagraphCenterAligned(
        Paragraph paragraph)
    {
        SetParagraphJustification(
            paragraph,
            JustificationValues.Center);
    }

    private static void SetParagraphRightAligned(
        Paragraph paragraph)
    {
        SetParagraphJustification(
            paragraph,
            JustificationValues.Right);
    }

    private static void SetParagraphJustification(
        Paragraph paragraph,
        JustificationValues value)
    {
        var properties =
            paragraph.ParagraphProperties;

        if (properties is null)
        {
            properties = new ParagraphProperties();
            paragraph.PrependChild(properties);
        }

        var justification =
            properties.GetFirstChild<Justification>();

        if (justification is null)
        {
            properties.Append(
                new Justification
                {
                    Val = value
                });
        }
        else
        {
            justification.Val =
                value;
        }
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
