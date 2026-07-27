using TaxMate.Infrastructure.Word;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Tests;

public class S2aHkdWordServiceTests
{
    [Fact]
    public async Task GenerateDocxAsync_ReturnsValidDocxPackage()
    {
        var service = new S2aHkdWordService();
        var model = new S2aHkdDocumentModel
        {
            Header = new S2aHkdHeaderModel
            {
                BusinessName = "ABC",
                Address = "44a Vườn Lài",
                TaxCode = "12345566",
                DeclarationPeriod = "Quý I/2026"
            },
            Groups =
            [
                new S2aHkdCategoryGroupModel
                {
                    GroupNumber = 1,
                    CategoryName = "Bán tạp hóa",
                    VatRate = 1m,
                    PitRate = 0.5m,
                    Lines =
                    [
                        new S2aHkdLineModel
                        {
                            DocumentNumber = "TM001",
                            TransactionDate = new DateTime(2026, 3, 15),
                            Description = "Dầu ăn",
                            Amount = 30_000m
                        }
                    ],
                    Subtotal = 30_000m,
                    VatTax = 300m,
                    PitTax = 150m
                }
            ],
            Footer = new S2aHkdFooterModel
            {
                TotalVatTax = 300m,
                TotalPitTax = 150m,
                ExportDate = new DateTime(2026, 3, 31)
            }
        };

        var bytes = await service.GenerateDocxAsync(model);

        Assert.NotEmpty(bytes);
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }
}
