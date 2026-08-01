using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Globalization;
using TaxMate.Model.Documents.Tax;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Infrastructure.Documents.Tax;

public class OpenXmlS1aDocumentGenerator : IS1aDocumentGenerator
{
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

            // Define standard font for the whole document
            SetDocumentDefaultFont(mainPart, "Times New Roman");

            // Header Section
            AddHeaderSection(body, model);
            
            // Title Section
            AddTitleSection(body, model);

            // Unit Section
            AddUnitSection(body, model);

            // Data Table
            AddDataTable(body, model);

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

    private void AddHeaderSection(Body body, S1aDocumentModel model)
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
        
        // Left Cell (Business Info)
        var leftCell = new TableCell();
        var leftCellProp = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2500" });
        leftCell.AppendChild(leftCellProp);
        
        AddParagraphToCell(leftCell, $"HỘ, CÁ NHÂN KINH DOANH: {model.BusinessName}", isBold: true);
        AddParagraphToCell(leftCell, $"Địa chỉ: {model.Address}", isBold: true);
        AddParagraphToCell(leftCell, $"Mã số thuế: {model.TaxCode}", isBold: true);
        tr.AppendChild(leftCell);

        // Right Cell (Template Info)
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
        
        // Add an empty paragraph as spacing
        body.AppendChild(new Paragraph(new Run(new Text(""))));
    }

    private void AddTitleSection(Body body, S1aDocumentModel model)
    {
        var titlePara = CreateParagraph("SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ", isBold: true, size: 28, alignment: JustificationValues.Center);
        body.AppendChild(titlePara);

        var locationPara = CreateParagraph($"Địa điểm kinh doanh: {model.BusinessLocation}", size: 24, alignment: JustificationValues.Center);
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

    private void AddDataTable(Body body, S1aDocumentModel model)
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

        // Header Rows
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

        // Data Rows
        decimal totalAmount = 0;
        foreach (var line in model.Lines)
        {
            var tr = new TableRow();
            AddCell(tr, line.Date, alignment: JustificationValues.Center);
            AddCell(tr, line.Description, alignment: JustificationValues.Left);
            
            var amountStr = line.RevenueAmount == 0 ? string.Empty : line.RevenueAmount.ToString("#,##0.##", CultureInfo.GetCultureInfo("vi-VN"));
            AddCell(tr, amountStr, alignment: JustificationValues.Right);
            
            table.AppendChild(tr);
            totalAmount += line.RevenueAmount;
        }
        
        // Add some empty rows to make the table look complete like the sample
        for (int i = 0; i < 5; i++)
        {
            var tr = new TableRow();
            AddCell(tr, "");
            AddCell(tr, "");
            AddCell(tr, "");
            table.AppendChild(tr);
        }

        // Footer Row
        var footerRow = new TableRow();
        
        var mergedLeftCell = new TableCell();
        var mergedLeftCellProp = new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "3500" },
            new GridSpan { Val = 2 }
        );
        mergedLeftCell.AppendChild(mergedLeftCellProp);
        AddParagraphToCell(mergedLeftCell, "Tổng cộng", isBold: true, alignment: JustificationValues.Center);
        footerRow.AppendChild(mergedLeftCell);

        var totalAmountStr = totalAmount == 0 ? "0" : totalAmount.ToString("#,##0.##", CultureInfo.GetCultureInfo("vi-VN"));
        AddCell(footerRow, totalAmountStr, isBold: true, alignment: JustificationValues.Right, widthPct: "1500");
        
        table.AppendChild(footerRow);
        
        body.AppendChild(table);
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
        // Minimal spacing
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
}
