using System.Globalization;
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
            foreach (var text in body.Descendants<Text>())
                if (replacements.TryGetValue(text.Text, out var value)) text.Text = value;
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

    private static string Check(bool value) => value ? "☒" : "☐";
    private static string Money(decimal value) => value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    private static string Date(DateTime? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
    private static string SignatureDate(DateTime? value) => value is null
        ? string.Empty
        : $"..., ngày {value.Value:dd} tháng {value.Value:MM} năm {value.Value:yyyy}";
    private static string Safe(string value) => string.Concat(value.Where(char.IsLetterOrDigit));
}
