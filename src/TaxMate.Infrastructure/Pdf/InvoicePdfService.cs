using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure.Pdf;

public class InvoicePdfService : IInvoicePdfService
{
    private readonly HttpClient _httpClient = new HttpClient();

    public async Task<byte[]> GeneratePdfAsync(InvoicePdfData data)
    {
        byte[]? qrImageBytes = null;
        if (!string.IsNullOrEmpty(data.QRCodeUrl))
        {
            try
            {
                qrImageBytes = await _httpClient.GetByteArrayAsync(data.QRCodeUrl);
            }
            catch
            {
                // Fallback gracefully if image download fails
            }
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(data.BusinessName).Bold().FontSize(13).FontColor(Colors.Blue.Darken2);
                    if (!string.IsNullOrEmpty(data.Address))
                    {
                        col.Item().Text($"Địa chỉ: {data.Address}").FontColor(Colors.Grey.Darken2).FontSize(8);
                    }
                    var contactInfo = new List<string>();
                    if (!string.IsNullOrEmpty(data.TaxCode)) contactInfo.Add($"MST: {data.TaxCode}");
                    if (!string.IsNullOrEmpty(data.Phone)) contactInfo.Add($"SĐT: {data.Phone}");
                    if (contactInfo.Any())
                    {
                        col.Item().Text(string.Join(" | ", contactInfo)).FontColor(Colors.Grey.Darken2).FontSize(8);
                    }

                    col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                    col.Item().Text("HÓA ĐƠN BÁN HÀNG").Bold().FontSize(14).AlignCenter().FontColor(Colors.Blue.Darken4);
                    col.Item().Text($"Số: {data.InvoiceNumber}").AlignCenter().FontColor(Colors.Grey.Darken3).FontSize(9);
                    col.Item().Text($"Ngày phát hành: {data.IssueDate.AddHours(7):dd/MM/yyyy HH:mm}").AlignCenter().FontColor(Colors.Grey.Darken3).FontSize(8);
                    col.Item().PaddingBottom(5);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(20); // STT
                            columns.RelativeColumn(3);  // Tên sản phẩm
                            columns.ConstantColumn(30); // ĐVT
                            columns.RelativeColumn(1.2f); // Đơn giá
                            columns.RelativeColumn(0.8f);  // Số lượng
                            columns.RelativeColumn(1.5f); // Thành tiền
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("STT").Bold().FontSize(8);
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Tên sản phẩm").Bold().FontSize(8);
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("ĐVT").Bold().FontSize(8);
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Đơn giá").Bold().AlignRight().FontSize(8);
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("SL").Bold().AlignRight().FontSize(8);
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("T.Tiền").Bold().AlignRight().FontSize(8);
                        });

                        for (int i = 0; i < data.Items.Count; i++)
                        {
                            var item = data.Items[i];
                            var index = i + 1;
                            
                            table.Cell().Padding(3).Text(index.ToString()).FontSize(8);
                            table.Cell().Padding(3).Text(item.ProductName).FontSize(8);
                            table.Cell().Padding(3).Text(item.Unit ?? "-").FontSize(8);
                            table.Cell().Padding(3).Text(item.UnitPrice.ToString("N0") + "đ").AlignRight().FontSize(8);
                            table.Cell().Padding(3).Text(item.Quantity.ToString("G")).AlignRight().FontSize(8);
                            table.Cell().Padding(3).Text(item.LineTotal.ToString("N0") + "đ").AlignRight().FontSize(8);
                        }
                    });

                    col.Item().PaddingTop(5).AlignRight().Width(180).Column(totalsCol =>
                    {
                        totalsCol.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Cộng tiền hàng:").AlignRight().FontSize(8);
                            row.ConstantItem(80).Text(data.SubTotal.ToString("N0") + " đ").AlignRight().FontSize(8);
                        });
                        
                        if (data.DiscountAmount > 0)
                        {
                            totalsCol.Item().Row(row =>
                            {
                                var discountStr = "Giảm giá:";
                                if (data.DiscountType == "Percentage" && data.DiscountValue.HasValue)
                                {
                                    discountStr = $"Giảm giá ({data.DiscountValue.Value}%):";
                                }
                                row.RelativeItem().Text(discountStr).AlignRight().FontSize(8).FontColor(Colors.Red.Medium);
                                row.ConstantItem(80).Text($"-{data.DiscountAmount.ToString("N0")} đ").AlignRight().FontSize(8).FontColor(Colors.Red.Medium);
                            });
                        }

                        if (data.SurchargeAmount > 0)
                        {
                            totalsCol.Item().Row(row =>
                            {
                                var surchargeStr = string.IsNullOrEmpty(data.SurchargeName) ? "Phụ thu:" : $"{data.SurchargeName}:";
                                row.RelativeItem().Text(surchargeStr).AlignRight().FontSize(8);
                                row.ConstantItem(80).Text($"+{data.SurchargeAmount.ToString("N0")} đ").AlignRight().FontSize(8);
                            });
                        }

                        totalsCol.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);

                        totalsCol.Item().Row(row =>
                        {
                            row.RelativeItem().Text("TỔNG CỘNG:").Bold().AlignRight().FontSize(10).FontColor(Colors.Blue.Darken3);
                            row.ConstantItem(80).Text(data.TotalAmount.ToString("N0") + " đ").Bold().AlignRight().FontSize(10).FontColor(Colors.Blue.Darken3);
                        });
                    });

                    if (qrImageBytes != null)
                    {
                        col.Item().PaddingTop(10).AlignCenter().Width(100).Column(qrCol =>
                        {
                            qrCol.Item().Image(qrImageBytes);
                            qrCol.Item().PaddingTop(2).Text("Quét mã để chuyển khoản").AlignCenter().FontSize(7).Italic().FontColor(Colors.Grey.Darken1);
                        });
                        
                        col.Item().AlignCenter().Column(bankCol =>
                        {
                            bankCol.Item().Text($"{data.BankName}").AlignCenter().Bold().FontSize(8);
                            bankCol.Item().Text($"STK: {data.AccountNumber} | {data.AccountName}").AlignCenter().FontSize(8);
                        });
                    }
                });

                page.Footer().AlignCenter().PaddingTop(10).Text(x =>
                {
                    x.Span("Cảm ơn Quý khách! Hẹn gặp lại!").Italic().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }
}
