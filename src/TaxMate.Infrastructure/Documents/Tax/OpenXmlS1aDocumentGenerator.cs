using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Globalization;
using TaxMate.Model.Documents.Tax;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Infrastructure.Documents.Tax;

public class OpenXmlS1aDocumentGenerator : IS1aDocumentGenerator
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public Task<TaxDeclarationGeneratedFile> GenerateAsync(
        S1aDocumentModel model,
        CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        
        using (var document = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            SetDocumentDefaultFont(mainPart, "Times New Roman");

            for (var i = 0; i < model.Businesses.Count; i++)
            {
                if (i > 0)
                {
                    body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                }

                var business = model.Businesses[i];
                AddHeaderSection(body, model, business);
                AddTitleSection(body, model, business);
                AddUnitSection(body, model);
                AddDataTable(body, business);
            }

            mainPart.Document.Save();
        }

        return Task.FromResult(new TaxDeclarationGeneratedFile
        {
            Content = memoryStream.ToArray(),
            FileName = $"S1a-HKD_{model.TaxCode}_{model.DeclarationPeriod.Replace(" ", "_").Replace("/", "_")}.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        });
    }

    private void SetDocumentDefaultFont(MainDocumentPart mainPart, string fontName)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();
        var docDefaults = new DocDefaults();
        var runPropertiesDefault = new RunPropertiesDefault();
        var runPropertiesBaseStyle = new RunPropertiesBaseStyle();
        
        var runFonts = new RunFonts { Ascii = fontName, HighAnsi = fontName, ComplexScript = fontName, EastAsia = fontName };
        runPropertiesBaseStyle.AppendChild(runFonts);
        runPropertiesDefault.AppendChild(runPropertiesBaseStyle);
        docDefaults.AppendChild(runPropertiesDefault);
        styles.AppendChild(docDefaults);
        stylesPart.Styles = styles;
    }

    private void AddHeaderSection(Body body, S1aDocumentModel model, S1aBusinessSectionModel business)
    {
        var table = new Table();

        var tblProp = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None }
            )
        );
        table.AppendChild(tblProp);

        var tr = new TableRow();
        
        var leftCell = new TableCell();
        var leftCellProp = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2500" });
        leftCell.AppendChild(leftCellProp);
        
        AddParagraphToCell(leftCell, $"HỘ, CÁ NHÂN KINH DOANH: {business.BusinessName}", isBold: true);
        AddParagraphToCell(leftCell, $"Địa chỉ: {business.Address}", isBold: true);
        AddParagraphToCell(leftCell, $"Mã số thuế: {model.TaxCode}", isBold: true);
        tr.AppendChild(leftCell);

        var rightCell = new TableCell();
        var rightCellProp = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2500" });
        rightCell.AppendChild(rightCellProp);

        AddParagraphToCell(rightCell, "Mẫu số S1a-HKD", isBold: true, alignment: JustificationValues.Center);
        AddParagraphToCell(rightCell, "(Kèm theo Thông tư số 152/2025/TT-BTC", isItalic: true, alignment: JustificationValues.Center);
        AddParagraphToCell(rightCell, "ngày 31 tháng 12 năm 2025 của Bộ trưởng", isItalic: true, alignment: JustificationValues.Center);
        AddParagraphToCell(rightCell, "Bộ Tài chính)", isItalic: true, alignment: JustificationValues.Center);
        
        tr.AppendChild(rightCell);
        table.AppendChild(tr);
        body.AppendChild(table);
        
        body.AppendChild(new Paragraph(new Run(new Text(""))));
    }

    private void AddTitleSection(Body body, S1aDocumentModel model, S1aBusinessSectionModel business)
    {
        var titlePara = CreateParagraph("SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ", isBold: true, size: 28, alignment: JustificationValues.Center);
        body.AppendChild(titlePara);

        var locationPara = CreateParagraph($"Địa điểm kinh doanh: {business.BusinessLocation}", size: 24, alignment: JustificationValues.Center);
        body.AppendChild(locationPara);

        var periodPara = CreateParagraph($"Kỳ kê khai: {model.DeclarationPeriod}", size: 24, alignment: JustificationValues.Center);
        body.AppendChild(periodPara);

        body.AppendChild(new Paragraph(new Run(new Text(""))));
    }

    private void AddUnitSection(Body body, S1aDocumentModel model)
    {
        var unitPara = CreateParagraph($"Đơn vị tính: {model.Unit}", isItalic: true, alignment: JustificationValues.Right);
        body.AppendChild(unitPara);
    }

    private void AddDataTable(Body body, S1aBusinessSectionModel business)
    {
        var table = new Table();
        
        var tblProp = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 6 },
                new BottomBorder { Val = BorderValues.Single, Size = 6 },
                new LeftBorder { Val = BorderValues.Single, Size = 6 },
                new RightBorder { Val = BorderValues.Single, Size = 6 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }
            )
        );
        table.AppendChild(tblProp);

        var headerRow1 = new TableRow();
        AddCell(headerRow1, "Ngày tháng", isBold: true, alignment: JustificationValues.Center, widthPct: "1000");
        AddCell(headerRow1, "Diễn giải", isBold: true, alignment: JustificationValues.Center, widthPct: "2500");
        AddCell(headerRow1, "Số tiền", isBold: true, alignment: JustificationValues.Center, widthPct: "1500");
        table.AppendChild(headerRow1);

        var headerRow2 = new TableRow();
        AddCell(headerRow2, "A", isBold: true, alignment: JustificationValues.Center, widthPct: "1000");
        AddCell(headerRow2, "B", isBold: true, alignment: JustificationValues.Center, widthPct: "2500");
        AddCell(headerRow2, "1", isBold: true, alignment: JustificationValues.Center, widthPct: "1500");
        table.AppendChild(headerRow2);

        decimal totalAmount = 0;
        foreach (var line in business.Lines)
        {
            var tr = new TableRow();
            AddCell(tr, line.Date, alignment: JustificationValues.Center);
            AddCell(tr, line.Description, alignment: JustificationValues.Left);
            
            var amountStr = line.RevenueAmount == 0 ? string.Empty : FormatAmount(line.RevenueAmount);
            AddCell(tr, amountStr, alignment: JustificationValues.Right);
            
            table.AppendChild(tr);
            totalAmount += line.RevenueAmount;
        }
        
        for (int i = 0; i < 5; i++)
        {
            var tr = new TableRow();
            AddCell(tr, "");
            AddCell(tr, "");
            AddCell(tr, "");
            table.AppendChild(tr);
        }

        AddSummaryRow(table, "Tổng cộng", FormatAmount(totalAmount), isBold: true, labelAlign: JustificationValues.Center);
        AddSummaryRow(table, $"Thuế GTGT ({FormatRate(business.VatRate)}%)", FormatAmount(business.VatTax), isBold: false);
        AddSummaryRow(table, $"Thuế TNCN ({FormatRate(business.PitRate)}%)", FormatAmount(business.PitTax), isBold: false);
        AddSummaryRow(table, "Tổng số thuế GTGT phải trả", FormatAmount(business.VatTax), isBold: true);
        AddSummaryRow(table, "Tổng số thuế TNCN phải trả", FormatAmount(business.PitTax), isBold: true);
        
        body.AppendChild(table);
    }

    private void AddSummaryRow(
        Table table,
        string label,
        string amount,
        bool isBold,
        JustificationValues? labelAlign = null)
    {
        var row = new TableRow();

        var mergedLeftCell = new TableCell();
        mergedLeftCell.AppendChild(new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "3500" },
            new GridSpan { Val = 2 }
        ));
        AddParagraphToCell(
            mergedLeftCell,
            label,
            isBold: isBold,
            alignment: labelAlign ?? JustificationValues.Left);
        row.AppendChild(mergedLeftCell);

        AddCell(row, amount, isBold: isBold, alignment: JustificationValues.Right, widthPct: "1500");
        table.AppendChild(row);
    }

    private void AddCell(TableRow row, string text, bool isBold = false, JustificationValues? alignment = null, string? widthPct = null)
    {
        var align = alignment ?? JustificationValues.Left;
        var cell = new TableCell();
        if (widthPct != null)
        {
            var cellProp = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = widthPct });
            cell.AppendChild(cellProp);
        }
        
        AddParagraphToCell(cell, text, isBold: isBold, alignment: align);
        row.AppendChild(cell);
    }

    private void AddParagraphToCell(TableCell cell, string text, bool isBold = false, bool isItalic = false, int size = 24, JustificationValues? alignment = null)
    {
        var align = alignment ?? JustificationValues.Left;
        cell.AppendChild(CreateParagraph(text, isBold, isItalic, size, align));
    }

    private Paragraph CreateParagraph(string text, bool isBold = false, bool isItalic = false, int size = 24, JustificationValues? alignment = null)
    {
        var align = alignment ?? JustificationValues.Left;
        var para = new Paragraph();
        
        var pp = new ParagraphProperties();
        if (align != JustificationValues.Left)
        {
            pp.AppendChild(new Justification { Val = align });
        }
        pp.AppendChild(new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto });
        
        para.AppendChild(pp);

        var run = new Run();
        var rp = new RunProperties();
        
        if (isBold) rp.AppendChild(new Bold());
        if (isItalic) rp.AppendChild(new Italic());
        if (size != 24) rp.AppendChild(new FontSize { Val = size.ToString() });

        run.AppendChild(rp);
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        
        para.AppendChild(run);
        
        return para;
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("#,##0.##", ViCulture);

    private static string FormatRate(decimal rate) =>
        rate % 1 == 0 ? rate.ToString("0", ViCulture) : rate.ToString("0.##", ViCulture);
}
