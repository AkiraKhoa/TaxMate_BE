using TaxMate.Model.DTO.Tax;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public sealed class QttCalculationEngine : IQttCalculationEngine
{
    private const decimal OneBillion = 1_000_000_000m;
    private const decimal ThreeBillion = 3_000_000_000m;
    private const decimal FiftyBillion = 50_000_000_000m;
    private const decimal SmallPayableExemptionLimit = 50_000m;

    public QttCalculationPreviewResponse Calculate(QttPreviewResponse preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!preview.CanClose)
            throw new ConflictException("Dữ liệu quyết toán còn nội dung phải xử lý trước khi tính thuế.");

        var indicator09a = RoundVnd(preview.Revenue.Indicator09a);
        var indicator09b = RoundVnd(preview.Revenue.Indicator09b);
        var indicator09c = RoundVnd(preview.Revenue.Indicator09c);
        var indicator09 = indicator09a + indicator09b + indicator09c;
        if (indicator09 > FiftyBillion)
            throw new ConflictException("Doanh thu năm trên 50 tỷ đồng, ngoài phạm vi TaxMate hỗ trợ.");

        var indicator10a = RoundVnd(preview.Expenses.Indicator10a);
        var indicator10b = RoundVnd(preview.Expenses.Indicator10b);
        var indicator10c = RoundVnd(preview.Expenses.Indicator10c);
        var indicator10d = RoundVnd(preview.Expenses.Indicator10d);
        var indicator10LoanInterest = RoundVnd(
            preview.Expenses.Indicator10LoanInterest);
        var indicator10e = RoundVnd(preview.Expenses.Indicator10e);
        var indicator10 = indicator10a + indicator10b + indicator10c +
                          indicator10d + indicator10LoanInterest + indicator10e;
        var indicator11 = indicator09 - indicator10;
        var indicator12 = ResolveRate(preview.Eligibility, indicator09);
        var indicator13 = RoundVnd(Math.Max(indicator11, 0m) * indicator12 / 100m);
        var indicator14 = 0m;
        var indicator15 = RoundVnd(preview.PitPayments.Indicator15);
        var indicator16 = 0m;
        var netPit = indicator13 - indicator14 - indicator15 - indicator16;
        var indicator17 = Math.Max(netPit, 0m);
        var indicator18 = indicator17 > 0m && indicator17 <= SmallPayableExemptionLimit
            ? indicator17
            : 0m;
        var indicator19 = indicator17 - indicator18;
        var indicator20 = Math.Max(-netPit, 0m);

        // Chưa có lựa chọn của người dùng: mặc định an toàn là để toàn bộ
        // khoản nộp thừa bù trừ cho các kỳ sau.
        const decimal indicator22 = 0m;
        const decimal indicator23 = 0m;
        var indicator21 = indicator22 + indicator23;
        var indicator24 = indicator20 - indicator21;

        return new QttCalculationPreviewResponse
        {
            TaxYear = preview.TaxYear,
            Eligibility = preview.Eligibility,
            Indicators = new QttIndicators09To24(
                indicator09,
                indicator09a,
                indicator09b,
                indicator09c,
                indicator10,
                indicator10a,
                indicator10b,
                indicator10c,
                indicator10d,
                indicator10LoanInterest,
                indicator10e,
                indicator11,
                indicator12,
                indicator13,
                indicator14,
                indicator15,
                indicator16,
                indicator17,
                indicator18,
                indicator19,
                indicator20,
                indicator21,
                indicator22,
                indicator23,
                indicator24),
            InventoryTotals = new QttInventoryTotals31To34(
                RoundVnd(preview.Inventory.Indicator31OpeningValue),
                RoundVnd(preview.Inventory.Indicator32InboundValue),
                RoundVnd(preview.Inventory.Indicator33OutboundValue),
                RoundVnd(preview.Inventory.Indicator34EndingValue)),
            ApplicableRateReason = BuildRateReason(indicator09, indicator12),
            Outcome = indicator19 > 0m
                ? QttCalculationOutcomes.Payable
                : indicator20 > 0m
                    ? QttCalculationOutcomes.Overpaid
                    : QttCalculationOutcomes.Zero,
            DueDate = new DateTime(
                preview.TaxYear + 1,
                3,
                31,
                0,
                0,
                0,
                DateTimeKind.Unspecified),
            Warnings = preview.Warnings
        };
    }

    private static decimal ResolveRate(string eligibility, decimal revenue)
    {
        if (eligibility == QttEligibility.UnderOneBillionRefund ||
            revenue <= OneBillion)
            return 0m;
        if (revenue <= ThreeBillion)
            return 15m;
        if (revenue <= FiftyBillion)
            return 17m;
        throw new ConflictException("Không xác định được thuế suất trong phạm vi hỗ trợ.");
    }

    private static string BuildRateReason(decimal revenue, decimal rate) => rate switch
    {
        0m => "Doanh thu năm không quá 1 tỷ đồng; QTT này chỉ xử lý khoản PIT IncomeBased đã nộp thừa.",
        15m => "Doanh thu năm trên 1 tỷ đến 3 tỷ đồng nên áp dụng thuế suất 15%.",
        17m => "Doanh thu năm trên 3 tỷ đến 50 tỷ đồng nên áp dụng thuế suất 17%.",
        _ => $"Thuế suất {rate}% được xác định từ doanh thu năm {revenue:N0} đồng."
    };

    private static decimal RoundVnd(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero);
}
