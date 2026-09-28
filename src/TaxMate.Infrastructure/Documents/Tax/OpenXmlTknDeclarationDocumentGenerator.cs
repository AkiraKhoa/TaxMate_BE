using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TaxMate.Model.Documents.Tax;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Infrastructure.Documents.Tax;

public sealed class OpenXmlTknDeclarationDocumentGenerator : ITknDeclarationDocumentGenerator
{
    private readonly string _templatePath = Path.Combine(AppContext.BaseDirectory,
        "Templates", "Tax", "2026", "mau-01-tkn-cnkd.docx");

    public async Task<TaxDeclarationGeneratedFile> GenerateAsync(
        Form01TknCnkd2026Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Validate(snapshot);
        if (!File.Exists(_templatePath))
            throw new FileNotFoundException("Template 01/TKN-CNKD 2026 not found.", _templatePath);
        await using var output = new MemoryStream();
        await using (var template = File.OpenRead(_templatePath))
            await template.CopyToAsync(output, cancellationToken);
        output.Position = 0;
        using (var document = WordprocessingDocument.Open(output, true))
        {
            var mainDocumentPart = document.MainDocumentPart
                ?? throw new InvalidOperationException("The 01/TKN-CNKD template has no main document part.");
            var mainDocument = mainDocumentPart.Document
                ?? throw new InvalidOperationException("The 01/TKN-CNKD template has no document root.");
            var body = mainDocument.Body
                ?? throw new InvalidOperationException("The 01/TKN-CNKD template has no body.");
            var replacements = BuildReplacements(snapshot);
            foreach (var cell in body.Descendants<TableCell>())
            {
                var marker = cell.InnerText.Trim();
                if (marker.StartsWith("{{A", StringComparison.Ordinal)
                    && marker.Length > 3 && char.IsDigit(marker[3])
                    && replacements.TryGetValue(marker, out var value))
                    SetCellLines(cell, value, JustificationValues.Right);
            }
            ReplacePlaceholders(body, replacements);
            mainDocument.Save();
        }
        return new TaxDeclarationGeneratedFile
        {
            Content = output.ToArray(),
            FileName = $"01-TKN-CNKD_{Safe(snapshot.TaxCode)}_{snapshot.Year}_{snapshot.PeriodSelector}.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }

    private static Dictionary<string, string> BuildReplacements(Form01TknCnkd2026Snapshot snapshot)
    {
        var row08 = Aggregate(snapshot.SectionALines.Where(x => x.IndicatorCode == "08"));
        var total = Aggregate(snapshot.SectionALines);
        var result = new Dictionary<string, string>
        {
            ["{{YEAR}}"] = snapshot.Year.ToString(CultureInfo.InvariantCulture),
            ["{{PERIOD_YEAR}}"] = Check(snapshot.PeriodSelector == "Year"),
            ["{{PERIOD_H1}}"] = Check(snapshot.PeriodSelector == "FirstHalf"),
            ["{{PERIOD_H2}}"] = Check(snapshot.PeriodSelector == "SecondHalf"),
            ["{{INITIAL}}"] = Check(snapshot.DeclarationType == "Initial"),
            ["{{SUPPLEMENT}}"] = snapshot.SupplementNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["{{AT_OR_BELOW_1B}}"] = Check(snapshot.IsAtOrBelowOneBillion),
            ["{{NEW_BUSINESS}}"] = Check(snapshot.IsNewBusinessAtOrBelowOneBillion),
            ["{{TAXPAYER_NAME}}"] = snapshot.TaxpayerName,
            ["{{TAX_CODE}}"] = snapshot.TaxCode,
            ["{{AUTHORIZED_NAME}}"] = snapshot.AuthorizedDeclarerName ?? string.Empty,
            ["{{AUTHORIZED_TAX_CODE}}"] = snapshot.AuthorizedDeclarerTaxCode ?? string.Empty,
            ["{{TAX_AGENT_NAME}}"] = snapshot.TaxAgentName ?? string.Empty,
            ["{{TAX_AGENT_TAX_CODE}}"] = snapshot.TaxAgentTaxCode ?? string.Empty,
            ["{{TAX_AGENT_CONTRACT}}"] = snapshot.TaxAgentContractNumber ?? string.Empty,
            ["{{TAX_AGENT_CONTRACT_DATE}}"] = Date(snapshot.TaxAgentContractDate),
            ["{{DECLARATION_DATE}}"] = SignatureDate(snapshot.GeneratedAt)
        };
        AddRow(result, "08", row08);
        foreach (var code in new[] { "09", "10", "11", "12" }) AddRow(result, code, Aggregate([]));
        AddRow(result, "13", total);
        return result;
    }

    private static void AddRow(Dictionary<string, string> target, string code, decimal[] values)
    {
        for (var index = 0; index < values.Length; index++)
            target[$"{{{{A{code}_{index + 1}}}}}"] = Money(values[index]);
    }

    private static decimal[] Aggregate(IEnumerable<Form01TknCnkd2026LineSnapshot> lines)
    {
        var items = lines.ToList();
        return [items.Sum(x => x.TotalRevenue), items.Sum(x => x.VatNonTaxableRevenue),
            items.Sum(x => x.ZeroRatedVatRevenue), items.Sum(x => x.VatTaxAmount),
            items.Sum(x => x.PersonalIncomeTaxableRevenue),
            items.Sum(x => x.PersonalIncomeTaxDeductibleRevenue),
            items.Sum(x => x.PersonalIncomeTaxAmount), 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m];
    }

    private static void Validate(Form01TknCnkd2026Snapshot snapshot)
    {
        if (snapshot.SchemaVersion != 1 || snapshot.FormCode != "01/TKN-CNKD")
            throw new InvalidOperationException("Unsupported 01/TKN-CNKD snapshot schema or form code.");
        if (snapshot.PeriodSelector is not ("Year" or "FirstHalf" or "SecondHalf"))
            throw new InvalidOperationException("Invalid TKN period selector.");
        if (snapshot.WindowStart >= snapshot.WindowEnd)
            throw new InvalidOperationException("Invalid TKN revenue window.");
        if (snapshot.SectionALines.Any(x => x.IndicatorCode != "08"))
            throw new InvalidOperationException("The current TKN exporter only supports official activity indicator [08].");
        if (snapshot.SectionALines.Any(x => x.VatTaxAmount != 0m || x.PersonalIncomeTaxAmount != 0m))
            throw new InvalidOperationException("A <=1B TKN notice cannot contain tax payable amounts.");
    }

    private static void ReplacePlaceholders(Body body, Dictionary<string, string> replacements)
    {
        var orderedReplacements = replacements
            .OrderByDescending(r => r.Key.Length)
            .ToList();

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var fullText = paragraph.InnerText;
            if (!fullText.Contains("{{"))
                continue;

            var texts = paragraph.Descendants<Text>().ToList();
            if (texts.Count == 0)
                continue;

            // 1. Try direct replacement inside each Text element
            foreach (var text in texts)
            {
                if (string.IsNullOrEmpty(text.Text) || !text.Text.Contains("{{"))
                    continue;

                foreach (var (marker, value) in orderedReplacements)
                {
                    if (text.Text.Contains(marker))
                    {
                        text.Text = text.Text.Replace(marker, value);
                        text.Space = SpaceProcessingModeValues.Preserve;
                    }
                }
            }

            // 2. If any placeholder was split across multiple runs / text nodes, merge and replace
            if (paragraph.InnerText.Contains("{{"))
            {
                var runs = paragraph.Elements<Run>().ToList();
                if (runs.Count > 0)
                {
                    var sb = new StringBuilder();
                    foreach (var run in runs)
                    {
                        foreach (var t in run.Elements<Text>())
                            sb.Append(t.Text);
                    }

                    var merged = sb.ToString();
                    foreach (var (marker, value) in orderedReplacements)
                    {
                        if (merged.Contains(marker))
                            merged = merged.Replace(marker, value);
                    }

                    var firstRun = runs[0];
                    var firstText = firstRun.GetFirstChild<Text>();
                    if (firstText is null)
                    {
                        firstText = new Text { Space = SpaceProcessingModeValues.Preserve };
                        firstRun.AppendChild(firstText);
                    }
                    firstText.Text = merged;
                    firstText.Space = SpaceProcessingModeValues.Preserve;

                    foreach (var extraText in firstRun.Elements<Text>().Skip(1).ToList())
                        extraText.Remove();

                    foreach (var otherRun in runs.Skip(1))
                    {
                        foreach (var t in otherRun.Elements<Text>().ToList())
                            t.Remove();
                    }
                }
            }
        }

        // 3. Fallback pass: catch any remaining text element anywhere in body
        foreach (var text in body.Descendants<Text>())
        {
            if (!string.IsNullOrEmpty(text.Text) && text.Text.Contains("{{"))
            {
                foreach (var (marker, value) in orderedReplacements)
                {
                    if (text.Text.Contains(marker))
                    {
                        text.Text = text.Text.Replace(marker, value);
                        text.Space = SpaceProcessingModeValues.Preserve;
                    }
                }
            }
        }
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

    private static string Check(bool value) => value ? "☒" : "☐";
    private static string Money(decimal value) => value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    private static string Date(DateTime? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
    private static string SignatureDate(DateTime? value) => value is null
        ? string.Empty
        : $"..., ngày {value.Value:dd} tháng {value.Value:MM} năm {value.Value:yyyy}";
    private static string Safe(string value) => string.Concat(value.Where(char.IsLetterOrDigit));
}
