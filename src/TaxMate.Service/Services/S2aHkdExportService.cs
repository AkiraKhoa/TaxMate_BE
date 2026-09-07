using Microsoft.Extensions.Options;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Options;

namespace TaxMate.Service.Services;

/// <summary>
/// Builds S2a-HKD sổ doanh thu as period revenue × industry % (operational book, not filed declaration).
/// Annual TNCN 500tr base adjustment is not applied inside sổ lines for v1.
/// </summary>
public class S2aHkdExportService : IS2aHkdExportService
{
    private readonly IBusinessProfileRepository _businessProfiles;
    private readonly IS2aHkdRepository _s2aHkdRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IGenericRepository<BusinessCategory> _categories;
    private readonly IS2aHkdWordService _wordService;
    private readonly decimal _s2aMaxRevenueThreshold;
    private readonly ITaxPolicyService _taxPolicyService;
    private readonly IOwnerRevenueProjector _ownerRevenue;

    public S2aHkdExportService(
        IBusinessProfileRepository businessProfiles,
        IS2aHkdRepository s2aHkdRepository,
        IReportRepository reportRepository,
        IGenericRepository<BusinessCategory> categories,
        IS2aHkdWordService wordService,
        IOptions<TaxSettings> taxSettings,
        ITaxPolicyService taxPolicyService,
        IOwnerRevenueProjector ownerRevenue)
    {
        _businessProfiles = businessProfiles;
        _s2aHkdRepository = s2aHkdRepository;
        _reportRepository = reportRepository;
        _categories = categories;
        _wordService = wordService;
        _s2aMaxRevenueThreshold = taxSettings.Value.S2aMaxRevenueThreshold;
        _taxPolicyService = taxPolicyService;
        _ownerRevenue = ownerRevenue;
    }

    public Task<S2aHkdDocumentModel> BuildDocumentModelAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        int quarter)
    {
        return BuildDocumentModelInternalAsync(ownerId, businessId, year, quarter);
    }

    public async Task<byte[]> ExportDocxAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        int quarter)
    {
        var model = await BuildDocumentModelInternalAsync(ownerId, businessId, year, quarter);
        return await _wordService.GenerateDocxAsync(model);
    }

    private async Task<S2aHkdDocumentModel> BuildDocumentModelInternalAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        int quarter)
    {
        ValidatePeriod(year, quarter);

        var business = await _businessProfiles.GetByIdWithOwnerAndCategoryAsync(businessId);
        if (business is null)
            throw new NotFoundException("Business profile not found.");

        if (business.OwnerId != ownerId)
            throw new UnauthorizedAccessException("You do not own this business.");

        if (string.IsNullOrWhiteSpace(business.Owner.TaxCode))
            throw new UnprocessableEntityException(
                S2aHkdErrorCodes.MissingTaxCode,
                "Mã số thuế chưa được cập nhật. Vui lòng cập nhật MST trước khi xuất sổ S2a.");

        var ytdRevenue = (await _ownerRevenue.ProjectCalendarYearAsync(ownerId, businessId, year)).TotalRevenue;
        var (_, quarterEndExclusive) = TaxPeriodWindow.GetQuarterWindow(
            year,
            quarter);
        var quarterPolicyDate = DateOnly.FromDateTime(
            quarterEndExclusive.AddDays(-1));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (quarterPolicyDate > today)
        {
            quarterPolicyDate = today;
        }

        var policy = await _taxPolicyService.GetEffectiveAsync(
            quarterPolicyDate);
        var minimumRevenueThreshold = policy.AnnualRevenueThreshold;
        if (ytdRevenue < minimumRevenueThreshold
            || ytdRevenue > _s2aMaxRevenueThreshold)
        {
            throw new UnprocessableEntityException(
                S2aHkdErrorCodes.NotEligible,
                $"Doanh thu năm {year} ({ytdRevenue:N0} đ) không nằm trong khoảng " +
                $"{minimumRevenueThreshold:N0}–{_s2aMaxRevenueThreshold:N0} VND để sử dụng sổ S2a.");
        }

        var (startDate, endDate) = TaxPeriodWindow.GetQuarterWindow(year, quarter);
        var aggregates = await _s2aHkdRepository.GetProductAggregatesAsync(
            businessId,
            startDate,
            endDate);

        if (aggregates.Count == 0)
        {
            throw new UnprocessableEntityException(
                S2aHkdErrorCodes.NoRevenue,
                $"Không có doanh thu trong {TaxPeriodWindow.FormatQuarterPeriod(year, quarter)}.");
        }

        var categories = (await _categories.GetAllAsync())
            .ToDictionary(x => x.BusinessCategoryId);

        var unmappedCodes = new List<string>();
        var grouped = new Dictionary<Guid, List<S2aHkdProductAggregate>>();

        foreach (var item in aggregates)
        {
            var categoryId = item.ProductBusinessCategoryId ?? business.MainCategoryId;
            if (!categoryId.HasValue)
            {
                unmappedCodes.Add(item.ProductCode);
                continue;
            }

            if (!grouped.TryGetValue(categoryId.Value, out var list))
            {
                list = [];
                grouped[categoryId.Value] = list;
            }

            list.Add(item);
        }

        if (unmappedCodes.Count > 0)
        {
            throw new UnprocessableEntityException(
                S2aHkdErrorCodes.MissingCategory,
                $"Sản phẩm chưa gán ngành nghề thuế: {string.Join(", ", unmappedCodes.Distinct())}.");
        }

        if (business.MainCategoryId is null && grouped.Count == 0)
        {
            throw new UnprocessableEntityException(
                S2aHkdErrorCodes.MissingCategory,
                "Hộ kinh doanh chưa cấu hình ngành nghề chính hoặc ngành nghề cho sản phẩm.");
        }

        var exportDate = GetVietnamToday();
        var groups = new List<S2aHkdCategoryGroupModel>();
        var groupNumber = 1;

        foreach (var (categoryId, lines) in grouped
                     .OrderBy(x => categories.TryGetValue(x.Key, out var c) ? c.Code : string.Empty))
        {
            if (!categories.TryGetValue(categoryId, out var category))
                continue;

            var detailLines = lines
                .Select(x => new S2aHkdLineModel
                {
                    DocumentNumber = x.ProductCode,
                    TransactionDate = x.LastTransactionDate,
                    Description = x.ProductName,
                    Amount = x.TotalAmount
                })
                .ToList();

            var subtotal = detailLines.Sum(x => x.Amount);
            var (vatTax, pitTax) = S2aHkdTaxCalculator.CalculateGroupTaxes(
                subtotal,
                category.VatRate,
                category.PitRate);

            groups.Add(new S2aHkdCategoryGroupModel
            {
                GroupNumber = groupNumber++,
                CategoryId = categoryId,
                CategoryName = category.Name,
                CategoryCode = category.Code,
                VatRate = category.VatRate,
                PitRate = category.PitRate,
                Lines = detailLines,
                Subtotal = subtotal,
                VatTax = vatTax,
                PitTax = pitTax
            });
        }

        return new S2aHkdDocumentModel
        {
            Header = new S2aHkdHeaderModel
            {
                BusinessName = business.BusinessName,
                Address = business.Address ?? string.Empty,
                TaxCode = business.Owner.TaxCode!,
                DeclarationPeriod = TaxPeriodWindow.FormatQuarterPeriod(year, quarter),
                Unit = "Đồng"
            },
            Groups = groups,
            Footer = new S2aHkdFooterModel
            {
                TotalVatTax = groups.Sum(x => x.VatTax),
                TotalPitTax = groups.Sum(x => x.PitTax),
                ExportDate = exportDate
            }
        };
    }

    private static void ValidatePeriod(int year, int quarter)
    {
        if (year < 2000 || year > 2100)
            throw new BadRequestException("Invalid year.");

        if (quarter is < 1 or > 4)
            throw new BadRequestException("Quarter must be between 1 and 4.");
    }

    private static DateTime GetVietnamToday()
    {
        var vietnamOffset = TimeSpan.FromHours(7);
        var vietnamNow = DateTimeOffset.UtcNow.ToOffset(vietnamOffset);
        return vietnamNow.Date;
    }
}
